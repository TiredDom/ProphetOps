<template>
  <AppShell title="Forecast" description="Six-month demand outlook (Holt-Winters).">
    <section class="dss-page">
      <p v-if="loading" class="dash-note">Loading forecast…</p>
      <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="forecast-brief" :class="`brief-${data.insight.direction}`">
          <p class="brief-label">Demand forecast · {{ data.method }} · {{ data.horizon }}-month outlook</p>

          <p class="brief-answer">
            <span v-if="data.ok" class="brief-mark" aria-hidden="true">{{ signalIcon }}</span>
            <span>{{ headline }}</span>
          </p>

          <ul v-if="supporting.length" class="brief-notes">
            <li v-for="note in supporting" :key="note.kind">{{ note.text }}</li>
          </ul>

          <details v-if="furtherDetail.length" class="brief-more">
            <summary>{{ furtherDetail.length }} more observations</summary>
            <ul class="brief-notes">
              <li v-for="note in furtherDetail" :key="note.kind">{{ note.text }}</li>
            </ul>
          </details>

          <div v-if="data.ok" class="brief-figures">
            <span class="brief-figure">
              <span class="brief-figure-label">Next month</span>
              <strong>{{ peso(nextValue) }}</strong>
            </span>
            <span class="brief-figure">
              <span class="brief-figure-label">Accuracy on past months</span>
              <strong>{{ data.accuracy }}% <em>MAPE {{ data.metrics.mape }}%</em></strong>
            </span>
          </div>
        </section>

        <p class="source-note" :class="{ 'source-live': data.dataSource.usingLiveRecords }" role="note">
          {{ sourceNote }}
        </p>

        <template v-if="data.ok">
          <div v-if="chart" ref="chartBox" class="forecast-chart">
            <svg
              :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
              class="forecast-svg"
              preserveAspectRatio="xMidYMid meet"
              role="img"
              aria-label="Recorded revenue history and six-month Holt-Winters forecast"
            >
              <g class="forecast-grid" aria-hidden="true">
                <line
                  v-for="tick in chart.ticks"
                  :key="`grid-${tick.value}`"
                  :x1="plotLeft"
                  :y1="tick.y"
                  :x2="plotRight"
                  :y2="tick.y"
                />
              </g>
              <g class="forecast-axis-y" aria-hidden="true">
                <text
                  v-for="tick in chart.ticks"
                  :key="`ylabel-${tick.value}`"
                  :x="plotLeft - 8"
                  :y="tick.y"
                  text-anchor="end"
                  dominant-baseline="middle"
                >{{ tick.label }}</text>
              </g>
              <line
                class="forecast-baseline"
                :x1="plotLeft"
                :y1="chart.baselineY"
                :x2="plotRight"
                :y2="chart.baselineY"
              />
              <polygon :points="chart.band" class="forecast-band" />
              <line
                :x1="chart.divider"
                :y1="plotTop"
                :x2="chart.divider"
                :y2="chart.baselineY"
                class="forecast-divider"
              />
              <polyline :points="chart.historyPoints" class="forecast-line-history" />
              <polyline :points="chart.forecastPoints" class="forecast-line-forecast" />
              <g class="forecast-axis-x" aria-hidden="true">
                <text
                  v-for="mark in chart.xLabels"
                  :key="`xlabel-${mark.index}`"
                  :x="mark.x"
                  :y="labelY"
                  text-anchor="middle"
                >{{ mark.text }}</text>
              </g>
            </svg>
            <p class="forecast-legend">
              <span><span class="legend-swatch history"></span>Recorded revenue</span>
              <span><span class="legend-swatch forecast"></span>Forecast</span>
              <span><span class="legend-swatch band"></span>~80% interval</span>
            </p>
          </div>

          <ul class="step-rows">
            <li v-for="s in data.steps" :key="s.step" class="step-row">
              <span class="step-month">
                {{ s.monthLabel }}
                <span v-if="s.value === peakValue" class="step-peak">Busiest</span>
              </span>
              <span class="step-figure">{{ peso(s.value) }}</span>
              <span class="step-bar" aria-hidden="true">
                <span class="step-bar-fill" :style="{ width: barWidth(s) }"></span>
              </span>
              <span class="step-margin">
                likely {{ peso(s.lower) }} – {{ peso(s.upper) }}
              </span>
            </li>
          </ul>
        </template>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { useContentWidth } from '../composables/useContentWidth';
import { api, type ForecastData, type ForecastStepView } from '../api';
import { peso, pesoCompact } from '../format';

// The viewBox tracks the rendered width so the chart draws at 1:1. Left to scale, a fixed
// viewBox magnifies every label and stroke inside it on a wide screen and grows the chart
// taller the wider the window gets.
const chartHeight = 226;
const plotLeft = 58;
const plotTop = 16;
const plotBottom = chartHeight - 30;
const labelY = plotBottom + 17;

const chartBox = ref<HTMLElement | null>(null);
const { width: chartWidth, attach: attachChart } = useContentWidth(chartBox, 760);
const plotRight = computed(() => chartWidth.value - 16);

const data = ref<ForecastData | null>(null);
const loading = ref(true);
const error = ref('');



function niceNum(range: number, round: boolean): number {
  const exponent = Math.floor(Math.log10(range));
  const fraction = range / Math.pow(10, exponent);
  let niceFraction: number;
  if (round) {
    if (fraction < 1.5) niceFraction = 1;
    else if (fraction < 3) niceFraction = 2;
    else if (fraction < 7) niceFraction = 5;
    else niceFraction = 10;
  } else {
    if (fraction <= 1) niceFraction = 1;
    else if (fraction <= 2) niceFraction = 2;
    else if (fraction <= 5) niceFraction = 5;
    else niceFraction = 10;
  }
  return niceFraction * Math.pow(10, exponent);
}

const nextValue = computed(() => data.value?.steps[0]?.value ?? 0);

const headline = computed(() => {
  const d = data.value;
  if (!d?.ok) return 'Not enough history to compute a Holt-Winters forecast yet.';
  return d.insight.notes[0]?.text ?? '';
});

const supporting = computed(() => (data.value?.insight.notes ?? []).slice(1, 4));
const furtherDetail = computed(() => (data.value?.insight.notes ?? []).slice(4));

const peakValue = computed(() => {
  const steps = data.value?.steps ?? [];
  return steps.length ? Math.max(...steps.map((s) => s.value)) : 0;
});

// Measured from zero, so bar length stays proportional to the figure beside it. Starting the
// scale at the smallest month would stretch a modest spread into a dramatic one.
function barWidth(s: ForecastStepView) {
  return peakValue.value > 0 ? (s.value / peakValue.value) * 100 + '%' : '0%';
}

// The interval is symmetrical around the forecast, so half its width is the whole story and
// reads as one figure rather than two the reader has to subtract.
function margin(s: ForecastStepView) {
  return Math.round((s.upper - s.lower) / 2);
}

const sourceNote = computed(() => {
  const d = data.value;
  if (!d) return '';
  const s = d.dataSource;
  if (s.usingLiveRecords) {
    const gaps = s.filledMonths > 0
      ? ` ${s.filledMonths} month${s.filledMonths === 1 ? '' : 's'} had no bookings and counted as zero.`
      : '';
    return `Based on your booking history — ${s.liveMonthsAvailable} months recorded.${gaps}`;
  }
  const need = Math.max(1, s.minimumMonths - s.recordedMonths);
  const have = s.recordedMonths === 1 ? '1 month' : `${s.recordedMonths} months`;
  return `Based on a reference seasonal pattern, not your own bookings yet. You have ${have} with bookings on record; ${need} more will switch this to your own history.`;
});

const signalIcon = computed(() => {
  const dir = data.value?.insight.direction;
  return dir === 'up' ? '▲' : dir === 'down' ? '▼' : '▬';
});

const chart = computed(() => {
  const d = data.value;
  if (!d || !d.ok || d.history.length === 0 || d.steps.length === 0) return null;

  const history = d.history.map((h) => h.value);
  const forecast = d.steps.map((s) => s.value);
  const lowers = d.steps.map((s) => s.lower);
  const uppers = d.steps.map((s) => s.upper);

  const all = [...history, ...forecast, ...lowers, ...uppers];
  const rawMin = Math.min(...all);
  const rawMax = Math.max(...all);

  const tickCount = 4;
  const spread = niceNum(Math.max(rawMax - rawMin, 1), false);
  const tickStep = niceNum(spread / tickCount, true);
  const niceMin = Math.floor(rawMin / tickStep) * tickStep;
  const niceMax = Math.ceil(rawMax / tickStep) * tickStep;
  const span = niceMax - niceMin || 1;

  const total = history.length + forecast.length;
  const stepX = (plotRight.value - plotLeft) / Math.max(total - 1, 1);
  const x = (i: number) => plotLeft + stepX * i;
  const y = (v: number) => plotTop + ((niceMax - v) / span) * (plotBottom - plotTop);

  const ticks: { value: number; y: number; label: string }[] = [];
  for (let v = niceMin; v <= niceMax + tickStep / 2; v += tickStep) {
    ticks.push({ value: v, y: y(v), label: pesoCompact(v) });
  }

  const bridge = history.length - 1;
  const historyPoints = history.map((v, i) => `${x(i)},${y(v)}`).join(' ');
  const forecastPoints = [
    `${x(bridge)},${y(history[bridge])}`,
    ...forecast.map((v, i) => `${x(history.length + i)},${y(v)}`),
  ].join(' ');

  const bandTop = uppers.map((v, i) => `${x(history.length + i)},${y(v)}`);
  const bandBottom = lowers.map((v, i) => `${x(history.length + i)},${y(v)}`).reverse();
  const band = [`${x(bridge)},${y(history[bridge])}`, ...bandTop, ...bandBottom].join(' ');

  const keep = new Set<number>([0, bridge, total - 1]);
  for (let i = 3; i <= total - 3; i += 3) {
    if (Math.abs(i - bridge) >= 2) keep.add(i);
  }
  const monthAt = (i: number) =>
    i < d.history.length ? d.history[i].month : d.steps[i - d.history.length].month;
  const xLabels = [...keep]
    .sort((a, b) => a - b)
    .map((i) => ({ index: i, x: x(i), text: monthAt(i) }));

  return {
    ticks,
    baselineY: y(niceMin),
    historyPoints,
    forecastPoints,
    band,
    divider: x(bridge),
    xLabels,
  };
});

onMounted(async () => {
  try {
    data.value = await api.forecast();
  } catch {
    error.value = 'Could not load the forecast.';
  } finally {
    loading.value = false;
    await nextTick();
    attachChart();
  }
});
</script>

<style scoped>
.forecast-brief {
  padding: 18px 24px 16px;
  background: var(--color-shell);
  color: var(--color-shell-text);
  border-radius: var(--radius-lg);
}

.brief-label {
  margin: 0;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.09em;
  text-transform: uppercase;
  color: var(--color-shell-muted);
}

.brief-answer {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin: 10px 0 0;
  font-size: 17px;
  line-height: 1.45;
  text-wrap: pretty;
}

.brief-mark {
  flex: none;
  font-size: 11px;
}

.brief-up .brief-mark {
  color: #6FDCAF;
}

.brief-down .brief-mark {
  color: #FF9E96;
}

.brief-flat .brief-mark {
  color: #F2CB7B;
}

.brief-notes {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 12px 0 0;
  padding: 0 0 0 20px;
  list-style: none;
  color: rgba(234, 240, 251, 0.82);
  font-size: 13.5px;
  line-height: 1.5;
  text-wrap: pretty;
}

.brief-notes li {
  position: relative;
}

.brief-notes li::before {
  content: '';
  position: absolute;
  left: -14px;
  top: 8px;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--color-shell-muted);
}

.brief-more {
  margin-top: 10px;
}

.brief-more summary {
  display: inline-block;
  color: var(--color-shell-text);
  font-size: 12.5px;
  font-weight: 600;
  cursor: pointer;
  border-radius: var(--radius-sm);
}

.brief-more summary:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.brief-more summary:focus-visible {
  outline: 2px solid var(--color-shell-text);
  outline-offset: 2px;
}

.brief-more .brief-notes {
  margin-top: 8px;
}

.brief-figures {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 40px;
  margin-top: 14px;
  padding-top: 13px;
  border-top: 1px solid var(--color-shell-border);
}

.brief-figure {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.brief-figure-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-shell-muted);
}

.brief-figure strong {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 21px;
  font-weight: 560;
  font-variant-numeric: lining-nums tabular-nums;
}

.brief-figure em {
  margin-left: 7px;
  font-family: var(--font-family);
  font-size: 12px;
  font-style: normal;
  font-weight: 500;
  color: var(--color-shell-muted);
}
.step-rows {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.step-row {
  display: grid;
  grid-template-columns: 186px 126px minmax(100px, 1fr) auto;
  align-items: center;
  gap: var(--space-4);
  padding: 8px 0;
  border-bottom: 1px solid var(--color-border-subtle);
}

.step-row:last-child {
  border-bottom: none;
}

.step-month {
  display: flex;
  align-items: baseline;
  gap: 8px;
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
}

.step-peak {
  color: var(--color-accent-ink);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.step-figure {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 17px;
  font-weight: 560;
  color: var(--color-text-primary);
  font-variant-numeric: lining-nums tabular-nums;
}

.step-bar {
  display: block;
  height: 8px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
  overflow: hidden;
}

.step-bar-fill {
  display: block;
  height: 100%;
  min-width: 2px;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
}

.step-margin {
  color: var(--color-text-muted);
  font-size: 12px;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

@media (max-width: 760px) {
  .step-row {
    grid-template-columns: 1fr auto;
    row-gap: var(--space-2);
  }

  .step-bar {
    grid-column: 1 / -1;
  }

  .step-margin {
    grid-column: 1 / -1;
  }
}

.source-note {
  margin: calc(var(--space-3) * -1) 0 0;
  padding: 0;
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.5;
}

.forecast-chart {
  padding: 14px 18px 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
}
.forecast-svg {
  display: block;
  width: 100%;
  height: auto;
}
.forecast-grid line {
  stroke: var(--color-border-subtle);
  stroke-width: 1;
}
.forecast-baseline {
  stroke: var(--color-border-strong);
  stroke-width: 1;
}
.forecast-axis-y text,
.forecast-axis-x text {
  fill: var(--color-text-muted);
  font-family: var(--font-family);
  font-size: 11px;
}
.forecast-band {
  fill: rgba(22, 52, 107, 0.12);
  stroke: none;
}
.forecast-divider {
  stroke: var(--color-border-strong);
  stroke-width: 1;
  stroke-dasharray: 3 3;
}
.forecast-line-history {
  fill: none;
  stroke: var(--color-primary);
  stroke-width: 2.5;
  stroke-linejoin: round;
  stroke-linecap: round;
}
.forecast-line-forecast {
  fill: none;
  stroke: var(--color-warning);
  stroke-width: 2.5;
  stroke-dasharray: 6 5;
  stroke-linejoin: round;
  stroke-linecap: round;
}
.forecast-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 24px;
  margin: 10px 0 0;
  font-size: 12px;
  color: var(--color-text-muted);
}
.legend-swatch {
  display: inline-block;
  width: 14px;
  height: 3px;
  margin-right: 0.4rem;
  vertical-align: middle;
  border-radius: 2px;
}
.legend-swatch.history {
  background: var(--color-primary);
}
.legend-swatch.forecast {
  background: var(--color-warning);
}
.legend-swatch.band {
  height: 12px;
  background: rgba(22, 52, 107, 0.2);
}
.dash-note {
  margin: 1.5rem 0;
  color: var(--color-text-muted);
}
.dash-error {
  color: var(--color-accent);
}
</style>
