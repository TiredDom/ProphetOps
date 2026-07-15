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

        <section class="analytics-panel">
          <div class="analytics-panel-head">
            <p class="analytics-panel-label">Sales history</p>
            <p class="analytics-panel-meta">Last {{ data.salesHistory.length }} months of revenue</p>
          </div>
          <svg class="sales-chart" viewBox="0 0 720 220" preserveAspectRatio="none" role="img" :aria-label="chartAriaLabel">
            <g v-for="bar in salesBars" :key="bar.label">
              <rect :x="bar.x" :y="bar.y" :width="bar.width" :height="bar.height" rx="3" class="sales-bar" />
              <text :x="bar.cx" y="212" text-anchor="middle" class="sales-axis">{{ bar.label }}</text>
            </g>
          </svg>
        </section>

        <div class="analytics-grid">
          <section class="analytics-panel">
            <div class="analytics-panel-head">
              <p class="analytics-panel-label">Package mix</p>
              <p class="analytics-panel-meta">Bookings by package</p>
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
              <p class="analytics-panel-meta">Bookings by payment</p>
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
            <p class="analytics-panel-meta">Gross revenue per route</p>
          </div>
          <div class="dss-table-frame">
            <table class="dss-table">
              <thead>
                <tr>
                  <th>Destination</th>
                  <th class="num">Gross revenue</th>
                  <th>Share</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in destinationBars" :key="row.label">
                  <td><strong>{{ row.label }}</strong></td>
                  <td class="num">{{ peso(row.value) }}</td>
                  <td><span class="bar-row-track"><span class="bar-row-fill" :style="{ width: row.pct + '%' }"></span></span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type AnalyticsData, type AnalyticsPoint } from '../api';

const data = ref<AnalyticsData | null>(null);
const loading = ref(true);
const error = ref('');

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function withPct(items: AnalyticsPoint[]): Array<AnalyticsPoint & { pct: number }> {
  const max = Math.max(1, ...items.map((i) => i.value));
  return items.map((i) => ({ ...i, pct: Math.round((i.value / max) * 100) }));
}

const packageBars = computed(() => withPct(data.value?.packageMix ?? []));
const paymentBars = computed(() => withPct(data.value?.paymentBreakdown ?? []));
const destinationBars = computed(() => withPct(data.value?.revenueByDestination ?? []));

const salesBars = computed(() => {
  const points = data.value?.salesHistory ?? [];
  const max = Math.max(1, ...points.map((p) => p.value));
  const band = 720 / (points.length || 1);
  const gap = Math.min(14, band * 0.3);
  return points.map((p, i) => {
    const height = Math.max(2, (p.value / max) * 176);
    return {
      label: p.label,
      x: i * band + gap / 2,
      cx: i * band + band / 2,
      y: 184 - height,
      width: band - gap,
      height,
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
  }
});
</script>

<style scoped>
.analytics-note {
  margin: 1.5rem 0;
  color: var(--color-text-secondary, rgba(21, 34, 27, 0.7));
}
.analytics-error {
  color: #B42318;
}
.analytics-panel {
  margin-top: 1.5rem;
  padding: 1.5rem 1.75rem;
  background: var(--color-surface, #FFFFFF);
  border: 1px solid var(--color-border, #E2DFD5);
  border-radius: 10px;
}
.analytics-panel-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.25rem;
}
.analytics-panel-label {
  margin: 0;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 1.15rem;
  color: var(--color-text-primary, #1C221B);
}
.analytics-panel-meta {
  margin: 0;
  font-size: 0.8rem;
  color: var(--color-text-muted, #5E675E);
}
.analytics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.5rem;
}
.analytics-grid .analytics-panel {
  margin-top: 1.5rem;
}
.sales-chart {
  display: block;
  width: 100%;
  height: 220px;
}
.sales-bar {
  fill: var(--color-primary, #1E6B4F);
}
.sales-axis {
  fill: var(--color-text-muted, #5E675E);
  font-size: 11px;
}
.bar-rows {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}
.bar-row {
  display: grid;
  grid-template-columns: minmax(90px, 1fr) 2fr auto;
  align-items: center;
  gap: 0.85rem;
}
.bar-row-label {
  font-size: 0.9rem;
  color: var(--color-text-secondary, #414A40);
}
.bar-row-track {
  display: block;
  width: 100%;
  height: 10px;
  border-radius: 999px;
  background: var(--color-surface-sunken, #EFEDE6);
  overflow: hidden;
}
.bar-row-fill {
  display: block;
  height: 100%;
  border-radius: 999px;
  background: var(--color-primary, #1E6B4F);
}
.bar-row-value {
  font-size: 0.9rem;
  color: var(--color-text-primary, #1C221B);
}
</style>
