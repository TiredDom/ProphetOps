<template>
  <AppShell title="Forecast" description="Six-month demand outlook (Holt-Winters).">
    <section class="dss-page">
      <p v-if="loading" class="dash-note">Loading forecast…</p>
      <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="forecast-panel">
          <p class="forecast-label">Demand forecast · {{ data.method }}</p>
          <p class="forecast-headline">{{ data.accuracy }}% accuracy on recorded months</p>
          <p class="forecast-detail">
            MAPE {{ data.metrics.mape }}% · {{ data.method }} · {{ data.horizon }}-month horizon
          </p>
        </section>

        <p class="source-note" :class="{ 'source-live': data.dataSource.usingLiveRecords }" role="note">
          {{ sourceNote }}
        </p>

        <section
          v-if="data.ok"
          class="decision-signal"
          :class="`signal-${data.insight.direction}`"
          role="note"
        >
          <span class="signal-mark" aria-hidden="true">{{ signalIcon }}</span>
          <p class="signal-text">{{ insightText }}</p>
        </section>

        <template v-if="data.ok">
          <section class="stat-band" aria-label="Forecast summary">
            <div class="stat-cell">
              <span class="stat-label">Average monthly error</span>
              <strong class="stat-value">{{ data.metrics.mape }}%</strong>
              <span class="stat-note">Typical error</span>
            </div>
            <div class="stat-cell">
              <span class="stat-label">Next month</span>
              <strong class="stat-value">{{ peso(nextValue) }}</strong>
              <span class="stat-note">Next month</span>
            </div>
            <div class="stat-cell">
              <span class="stat-label">Outlook</span>
              <strong class="stat-value">{{ data.horizon }} mo</strong>
              <span class="stat-note">Months ahead</span>
            </div>
          </section>

          <div v-if="chart" class="forecast-chart">
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

          <div class="dss-table-frame">
            <table class="dss-table">
              <thead>
                <tr>
                  <th>Step</th>
                  <th class="num">Forecast</th>
                  <th class="num">Low estimate</th>
                  <th class="num">High estimate</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="s in data.steps" :key="s.step">
                  <td><strong>Month {{ s.step }}</strong></td>
                  <td class="num"><strong>{{ peso(s.value) }}</strong></td>
                  <td class="num">{{ peso(s.lower) }}</td>
                  <td class="num">{{ peso(s.upper) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>

        <p v-else class="dash-note">
          Not enough history to compute a Holt-Winters forecast yet.
        </p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type ForecastData } from '../api';
import { peso, pesoCompact } from '../format';

const chartWidth = 720;
const chartHeight = 320;
const plotLeft = 58;
const plotRight = chartWidth - 20;
const plotTop = 20;
const plotBottom = chartHeight - 36;
const labelY = plotBottom + 20;

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

const insightText = computed(() => {
  const d = data.value;
  if (!d || !d.ok) return '';
  const ins = d.insight;
  const trend =
    ins.direction === 'up' ? 'trending upward' :
    ins.direction === 'down' ? 'trending downward' : 'holding steady';
  const magnitude =
    ins.direction === 'flat'
      ? 'projected to stay roughly flat over the next six months'
      : `projected ${ins.changePercent > 0 ? '+' : ''}${ins.changePercent}% over the next six months`;
  return `Demand is ${trend} — ${magnitude}, peaking in ${ins.peakMonth} (${peso(ins.peakValue)}), at about ${d.accuracy}% in-sample accuracy.`;
});

const sourceNote = computed(() => {
  const d = data.value;
  if (!d) return '';
  const s = d.dataSource;
  if (s.usingLiveRecords) {
    const gaps = s.filledMonths > 0
      ? ` ${s.filledMonths} month${s.filledMonths === 1 ? '' : 's'} had no bookings and counted as zero.`
      : '';
    return `Forecasting from your own booking records — ${s.liveMonthsAvailable} complete months.${gaps}`;
  }
  const have = s.liveMonthsAvailable;
  return `Showing the built-in sample series. Forecasting from your own records needs ${s.minimumMonths} complete months of bookings; you have ${have}.`;
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
  const stepX = (plotRight - plotLeft) / Math.max(total - 1, 1);
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
  }
});
</script>

<style scoped>
.forecast-panel {
  margin-bottom: 1.5rem;
  padding: 1.75rem 2rem;
  background: var(--color-shell);
  color: var(--color-shell-text);
  border-radius: 10px;
}
.forecast-label {
  margin: 0;
  font-size: 0.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgba(233, 237, 233, 0.6);
}
.forecast-headline {
  margin: 0.35rem 0 0.25rem;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 2.1rem;
  font-weight: 600;
}
.forecast-detail {
  margin: 0;
  color: rgba(233, 237, 233, 0.8);
  font-size: 0.95rem;
}
.source-note {
  margin: calc(var(--space-4) * -1) 0 0;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--tone-warning-border);
  border-radius: var(--radius-md);
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
  font-size: 12.5px;
  line-height: 1.5;
}

.source-note.source-live {
  border-color: var(--tone-primary-border);
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
}

.decision-signal {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  margin-bottom: 1.5rem;
  padding: 0.9rem 1.15rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}
.signal-mark {
  display: grid;
  place-items: center;
  flex-shrink: 0;
  width: 1.85rem;
  height: 1.85rem;
  border-radius: var(--radius-pill);
  color: #FFFFFF;
  font-size: 0.8rem;
  line-height: 1;
}
.signal-text {
  margin: 0;
  font-size: 1rem;
  color: var(--color-text-primary);
}
.signal-up {
  background: var(--tint-success);
  border-color: rgba(18, 128, 91, 0.24);
}
.signal-up .signal-mark {
  background: var(--color-success);
}
.signal-down {
  background: var(--tint-danger);
  border-color: rgba(192, 38, 31, 0.24);
}
.signal-down .signal-mark {
  background: var(--color-danger);
}
.signal-flat {
  background: var(--tint-warning);
  border-color: rgba(181, 115, 11, 0.24);
}
.signal-flat .signal-mark {
  background: var(--color-warning);
}
.forecast-chart {
  margin: 1.5rem 0;
  padding: 1.25rem 1.5rem;
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
  gap: 0.5rem 1.5rem;
  margin: 1rem 0 0;
  font-size: 0.8rem;
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
