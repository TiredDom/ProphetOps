<template>
  <AppShell title="Forecast" description="Six-month demand outlook (Holt-Winters).">
    <section class="dss-page">
      <p v-if="loading" class="dash-note">Loading forecast…</p>
      <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="forecast-panel">
          <p class="forecast-label">Demand forecast · {{ data.method }}</p>
          <p class="forecast-headline">{{ data.accuracy }}% in-sample fit</p>
          <p class="forecast-detail">
            MAPE {{ data.metrics.mape }}% · {{ data.method }} · {{ data.horizon }}-month horizon
          </p>
        </section>

        <template v-if="data.ok">
          <section class="stat-band" aria-label="Forecast summary">
            <div class="stat-cell">
              <span class="stat-label">In-sample MAPE</span>
              <strong class="stat-value">{{ data.metrics.mape }}%</strong>
              <span class="stat-note">Average percent error</span>
            </div>
            <div class="stat-cell">
              <span class="stat-label">Next month</span>
              <strong class="stat-value">{{ peso(nextValue) }}</strong>
              <span class="stat-note">Step 1 projection</span>
            </div>
            <div class="stat-cell">
              <span class="stat-label">Horizon</span>
              <strong class="stat-value">{{ data.horizon }} mo</strong>
              <span class="stat-note">Months projected</span>
            </div>
          </section>

          <div v-if="chart" class="forecast-chart">
            <svg
              :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
              class="forecast-svg"
              role="img"
              aria-label="Recorded revenue history and six-month Holt-Winters forecast"
            >
              <polygon :points="chart.band" class="forecast-band" />
              <line
                :x1="chart.divider"
                y1="0"
                :x2="chart.divider"
                :y2="chartHeight"
                class="forecast-divider"
              />
              <polyline :points="chart.historyPoints" class="forecast-line-history" />
              <polyline :points="chart.forecastPoints" class="forecast-line-forecast" />
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
                  <th class="num">Lower</th>
                  <th class="num">Upper</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="s in data.steps" :key="s.step">
                  <td><strong>M+{{ s.step }}</strong></td>
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

const chartWidth = 720;
const chartHeight = 260;
const padX = 44;
const padY = 24;

const data = ref<ForecastData | null>(null);
const loading = ref(true);
const error = ref('');

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

const nextValue = computed(() => data.value?.steps[0]?.value ?? 0);

const chart = computed(() => {
  const d = data.value;
  if (!d || !d.ok || d.history.length === 0 || d.steps.length === 0) return null;

  const history = d.history.map((h) => h.value);
  const forecast = d.steps.map((s) => s.value);
  const lowers = d.steps.map((s) => s.lower);
  const uppers = d.steps.map((s) => s.upper);

  const all = [...history, ...forecast, ...lowers, ...uppers];
  const min = Math.min(...all);
  const max = Math.max(...all);
  const span = max - min || 1;

  const total = history.length + forecast.length;
  const stepX = (chartWidth - padX * 2) / Math.max(total - 1, 1);
  const x = (i: number) => padX + stepX * i;
  const y = (v: number) => chartHeight - padY - ((v - min) / span) * (chartHeight - padY * 2);

  const bridge = history.length - 1;
  const historyPoints = history.map((v, i) => `${x(i)},${y(v)}`).join(' ');
  const forecastPoints = [
    `${x(bridge)},${y(history[bridge])}`,
    ...forecast.map((v, i) => `${x(history.length + i)},${y(v)}`),
  ].join(' ');

  const bandTop = uppers.map((v, i) => `${x(history.length + i)},${y(v)}`);
  const bandBottom = lowers.map((v, i) => `${x(history.length + i)},${y(v)}`).reverse();
  const band = [`${x(bridge)},${y(history[bridge])}`, ...bandTop, ...bandBottom].join(' ');

  return { historyPoints, forecastPoints, band, divider: x(bridge) };
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
  background: #15221B;
  color: #E9EDE9;
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
.forecast-chart {
  margin: 1.5rem 0;
  padding: 1.25rem 1.5rem;
  background: var(--color-surface, #FFFFFF);
  border: 1px solid rgba(21, 34, 27, 0.14);
  border-radius: 10px;
}
.forecast-svg {
  display: block;
  width: 100%;
  height: auto;
}
.forecast-band {
  fill: rgba(30, 107, 79, 0.12);
  stroke: none;
}
.forecast-divider {
  stroke: rgba(21, 34, 27, 0.2);
  stroke-width: 1;
  stroke-dasharray: 3 3;
}
.forecast-line-history {
  fill: none;
  stroke: var(--color-primary, #1E6B4F);
  stroke-width: 2.5;
  stroke-linejoin: round;
  stroke-linecap: round;
}
.forecast-line-forecast {
  fill: none;
  stroke: var(--color-warning, #9A6700);
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
  color: rgba(21, 34, 27, 0.7);
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
  background: var(--color-primary, #1E6B4F);
}
.legend-swatch.forecast {
  background: var(--color-warning, #9A6700);
}
.legend-swatch.band {
  height: 12px;
  background: rgba(30, 107, 79, 0.18);
}
.dash-note {
  margin: 1.5rem 0;
  color: rgba(21, 34, 27, 0.7);
}
.dash-error {
  color: #B42318;
}
</style>
