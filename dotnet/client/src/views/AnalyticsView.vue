<template>
  <AppShell title="Sales Analytics" description="Revenue trends and booking mix.">
    <section class="dss-page">
      <p v-if="loading" class="analytics-note">Loading live data…</p>
      <p v-else-if="error" class="analytics-note analytics-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="stat-band" aria-label="Sales totals">
          <div class="stat-cell">
            <span class="stat-label">Total revenue</span>
            <strong class="stat-value">{{ peso(data.totalRevenue) }}</strong>
            <span class="stat-note">Gross, all bookings</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Total bookings</span>
            <strong class="stat-value">{{ data.totalBookings }}</strong>
            <span class="stat-note">Saved records</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Average booking</span>
            <strong class="stat-value">{{ peso(data.averageBooking) }}</strong>
            <span class="stat-note">Revenue per booking</span>
          </div>
        </section>

        <section ref="chartBox" class="analytics-panel">
          <div class="analytics-panel-head">
            <p class="analytics-panel-label">Sales history</p>
            <p class="analytics-panel-meta">Last {{ data.salesHistory.length }} months of revenue</p>
          </div>
          <svg class="sales-chart" :viewBox="`0 0 ${chartWidth} ${chartHeight}`" preserveAspectRatio="xMidYMid meet" role="img" :aria-label="chartAriaLabel">
            <line class="sales-axis-line" x1="56" y1="18" x2="56" y2="180" />
            <g v-for="line in gridLines" :key="'grid-' + line.value">
              <line
                :class="['sales-gridline', { 'sales-gridline-base': line.value === 0 }]"
                x1="56"
                :y1="line.y"
                :x2="plotRight"
                :y2="line.y"
              />
              <text class="sales-ylabel" x="48" :y="line.y" text-anchor="end" dominant-baseline="middle">{{ line.label }}</text>
            </g>
            <g v-for="bar in salesBars" :key="bar.label">
              <rect class="sales-bar" :x="bar.x" :y="bar.y" :width="bar.width" :height="bar.height" rx="2">
                <title>{{ bar.tip }}</title>
              </rect>
              <text v-if="bar.showLabel" class="sales-xlabel" :x="bar.cx" y="198" text-anchor="middle">{{ bar.label }}</text>
            </g>
          </svg>
        </section>

        <div class="analytics-grid">
          <section class="analytics-panel">
            <div class="analytics-panel-head">
              <p class="analytics-panel-label">Package mix</p>
            </div>
            <div class="bar-rows">
              <div v-for="row in packageBars" :key="row.label" class="bar-row">
                <span class="bar-row-label">{{ row.label }}</span>
                <span class="bar-row-track"><span class="bar-row-fill" :style="{ width: row.pct + '%' }"></span></span>
                <strong class="bar-row-value">{{ row.value }}</strong>
              </div>
            </div>
          </section>

          <section class="analytics-panel">
            <div class="analytics-panel-head">
              <p class="analytics-panel-label">Payment status</p>
            </div>
            <div class="bar-rows">
              <div v-for="row in paymentBars" :key="row.label" class="bar-row">
                <span class="bar-row-label">{{ row.label }}</span>
                <span class="bar-row-track"><span class="bar-row-fill" :style="{ width: row.pct + '%' }"></span></span>
                <strong class="bar-row-value">{{ row.value }}</strong>
              </div>
            </div>
          </section>
        </div>

        <section class="analytics-panel">
          <div class="analytics-panel-head">
            <p class="analytics-panel-label">Revenue by destination</p>
          </div>
          <div class="bar-rows">
            <div v-for="row in destinationBars" :key="row.label" class="bar-row">
              <span class="bar-row-label">{{ row.label }}</span>
              <span class="bar-row-track"><span class="bar-row-fill" :style="{ width: row.pct + '%' }"></span></span>
              <strong class="bar-row-value">{{ peso(row.value) }}</strong>
            </div>
          </div>
        </section>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { useContentWidth } from '../composables/useContentWidth';
import { api, type AnalyticsData, type AnalyticsPoint } from '../api';
import { peso, pesoCompact } from '../format';

const data = ref<AnalyticsData | null>(null);
const loading = ref(true);
const error = ref('');

// The viewBox tracks the rendered width so the chart draws at 1:1. A fixed viewBox stretched
// to full width magnifies every label and bar inside it and grows taller as the window widens.
const chartHeight = 208;
const plotLeft = 56;
const plotTop = 18;
const plotBottom = 180;

const chartBox = ref<HTMLElement | null>(null);
const { width: chartWidth, attach: attachChart } = useContentWidth(chartBox, 640);
const plotRight = computed(() => chartWidth.value - 16);


function trimNumber(value: number): string {
  const rounded = Math.round(value * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}


function niceScale(peak: number, ticks: number): { max: number; step: number } {
  if (peak <= 0) return { max: ticks, step: 1 };
  const rough = peak / ticks;
  const power = Math.pow(10, Math.floor(Math.log10(rough)));
  const norm = rough / power;
  let factor: number;
  if (norm <= 1) factor = 1;
  else if (norm <= 2) factor = 2;
  else if (norm <= 2.5) factor = 2.5;
  else if (norm <= 5) factor = 5;
  else factor = 10;
  const step = factor * power;
  return { max: Math.ceil(peak / step) * step, step };
}

function withPct(items: AnalyticsPoint[]): Array<AnalyticsPoint & { pct: number }> {
  const max = Math.max(1, ...items.map((i) => i.value));
  return items.map((i) => ({ ...i, pct: Math.round((i.value / max) * 100) }));
}

const packageBars = computed(() => withPct(data.value?.packageMix ?? []));
const paymentBars = computed(() => withPct(data.value?.paymentBreakdown ?? []));
const destinationBars = computed(() => withPct(data.value?.revenueByDestination ?? []));

const salesScale = computed(() => {
  const points = data.value?.salesHistory ?? [];
  const peak = Math.max(0, ...points.map((p) => p.value));
  return niceScale(peak, 4);
});

const gridLines = computed(() => {
  const { max, step } = salesScale.value;
  const span = plotBottom - plotTop;
  const lines: Array<{ value: number; y: number; label: string }> = [];
  for (let value = 0; value <= max + step / 2; value += step) {
    lines.push({ value, y: plotBottom - (value / max) * span, label: pesoCompact(value) });
  }
  return lines;
});

const salesBars = computed(() => {
  const points = data.value?.salesHistory ?? [];
  const count = points.length;
  const { max } = salesScale.value;
  const span = plotBottom - plotTop;
  const band = count ? (plotRight.value - plotLeft) / count : 0;
  const barWidth = Math.min(38, band * 0.56);
  const stride = band > 0 && band < 40 ? 2 : 1;
  return points.map((p, i) => {
    const height = max > 0 ? Math.max(1, (p.value / max) * span) : 1;
    const center = plotLeft + band * i + band / 2;
    return {
      label: p.label,
      x: center - barWidth / 2,
      cx: center,
      y: plotBottom - height,
      width: barWidth,
      height,
      showLabel: i % stride === 0,
      tip: `${p.label} · ${peso(p.value)}`,
    };
  });
});

const chartAriaLabel = computed(() => {
  const points = data.value?.salesHistory ?? [];
  if (!points.length) return 'Bar chart of monthly revenue. No data recorded.';
  const listed = points.map((p) => `${p.label} ${peso(p.value)}`).join(', ');
  return `Bar chart of monthly revenue, oldest first: ${listed}`;
});

onMounted(async () => {
  try {
    data.value = await api.analytics();
  } catch {
    error.value = 'Could not load the analytics.';
  } finally {
    loading.value = false;
    await nextTick();
    attachChart();
  }
});
</script>

<style scoped>
.analytics-note {
  margin: 1.5rem 0;
  color: var(--color-text-secondary);
}
.analytics-error {
  color: var(--color-danger-ink);
}
.analytics-panel {
  padding: 16px 20px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
}
.analytics-panel-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 14px;
}
.analytics-panel-label {
  margin: 0;
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 17px;
  font-weight: 560;
  color: var(--color-text-primary);
}
.analytics-panel-meta {
  margin: 0;
  font-size: 12px;
  color: var(--color-text-muted);
}
.analytics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  align-items: start;
  gap: 20px;
}
.sales-chart {
  display: block;
  width: 100%;
  height: auto;
}
.sales-axis-line,
.sales-gridline {
  stroke: var(--color-border-subtle);
  stroke-width: 1;
}
.sales-gridline-base {
  stroke: var(--color-border);
}
.sales-ylabel,
.sales-xlabel {
  fill: var(--color-text-muted);
  font-family: var(--font-family);
  font-size: 11px;
  font-variant-numeric: lining-nums tabular-nums;
}
.sales-bar {
  fill: var(--color-primary);
  transition: fill var(--transition-fast);
}
.sales-bar:hover {
  fill: var(--color-primary-hover);
}
@media (prefers-reduced-motion: reduce) {
  .sales-bar {
    transition: none;
  }
}
.bar-rows {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.bar-row {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(48px, 1fr) auto;
  align-items: center;
  gap: 12px;
}
.bar-row-label {
  font-size: 13px;
  color: var(--color-text-secondary);
}
.bar-row-track {
  display: block;
  width: 100%;
  height: 10px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
  overflow: hidden;
}
.bar-row-fill {
  display: block;
  height: 100%;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
}
.bar-row-value {
  font-size: 0.9rem;
  color: var(--color-text-primary);
}
</style>
