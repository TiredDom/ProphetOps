<template>
  <div class="forecast-details-body">
    <section class="details-block step-panel" aria-labelledby="breakdown-title">
      <h3 id="breakdown-title" class="details-heading">Monthly breakdown</h3>

      <div class="table-scroll">
        <table class="dss-table breakdown-table">
          <thead>
            <tr>
              <th scope="col">Month</th>
              <th scope="col" class="num">Forecast {{ noun }}</th>
              <th scope="col" class="num wide-only">Lower estimate</th>
              <th scope="col" class="num wide-only">Upper estimate</th>
              <th scope="col" class="num">Change</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in rows" :key="row.step">
              <td>
                <span class="month-label">
                  <span class="month-full">{{ row.monthLabel }}</span>
                  <span class="month-short">{{ shortMonth(row.monthLabel) }}</span>
                </span>
                <span v-if="row.isPeak" class="month-note">Highest forecast</span>
              </td>
              <td class="num">
                <strong>{{ full(row.value) }}</strong>
                <span class="narrow-range">{{ short(row.lower) }} – {{ short(row.upper) }}</span>
              </td>
              <td class="num wide-only">{{ full(row.lower) }}</td>
              <td class="num wide-only">{{ full(row.upper) }}</td>
              <td class="num change" :class="row.changeDirection">
                <span v-if="row.changePercent != null" class="change-value">
                  <UiIcon
                    v-if="row.changeDirection !== 'flat'"
                    :name="row.changeDirection === 'up' ? 'up' : 'down'"
                    class="change-mark"
                  />
                  {{ row.changePercent > 0 ? '+' : '' }}{{ row.changePercent.toFixed(1) }}%
                </span>
                <span v-else class="change-none" aria-label="No comparison">–</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <p class="details-note">
        Lower and upper estimates form the approximate 80% prediction range. Change compares each month with the one
        before it; {{ rows[0]?.monthLabel }} compares with {{ lastRecordedMonth }}, the last recorded month.
      </p>
    </section>

    <section class="details-block model-block" aria-labelledby="model-title">
      <h3 id="model-title" class="details-heading">Model details</h3>

      <div class="model-grid">
        <dl class="model-list" aria-label="Forecast settings">
          <div class="model-row">
            <dt>Method</dt>
            <dd>{{ data.method || 'Holt-Winters' }} (additive)</dd>
          </div>
          <div class="model-row">
            <dt>Forecast period</dt>
            <dd>{{ data.horizon || 6 }} months</dd>
          </div>
          <div class="model-row">
            <dt>Seasonal cycle</dt>
            <dd>{{ data.seasonLength || 12 }} months</dd>
          </div>
          <div class="model-row">
            <dt>Measure</dt>
            <dd>{{ measureDescription(measure) }}</dd>
          </div>
          <div v-if="data.params" class="model-row">
            <dt>Smoothing settings</dt>
            <dd>α {{ fixed(data.params.alpha) }} · β {{ fixed(data.params.beta) }} · γ {{ fixed(data.params.gamma) }}</dd>
          </div>
        </dl>

        <div class="fit-block">
          <h4 class="fit-heading">Fit on recorded months</h4>
          <dl class="model-list">
            <div class="model-row">
              <dt>Average monthly miss (MAPE)</dt>
              <dd>{{ data.metrics?.mape != null ? data.metrics.mape.toFixed(1) + '%' : 'Not available' }}</dd>
            </div>
            <div class="model-row">
              <dt>{{ measure === 'revenue' ? 'Average miss in pesos (MAE)' : 'Average miss (MAE)' }}</dt>
              <dd>{{ miss(data.metrics?.mae ?? 0) }}</dd>
            </div>
            <div class="model-row">
              <dt>Root mean squared error (RMSE)</dt>
              <dd>{{ miss(data.metrics?.rmse ?? 0) }}</dd>
            </div>
            <div class="model-row">
              <dt>Months compared</dt>
              <dd>{{ data.metrics?.sampleSize ?? 0 }}</dd>
            </div>
            <div v-if="data.baselines?.seasonalNaiveMae" class="model-row">
              <dt>Same month last year, for comparison (MAE)</dt>
              <dd>{{ miss(data.baselines.seasonalNaiveMae) }}</dd>
            </div>
          </dl>
          <p class="details-note">
            These figures show how closely the model reproduces the months it learned from. They are not a test on
            months it has not seen.
          </p>
        </div>
      </div>

      <p v-if="measure === 'revenue'" class="details-note method-note">
        Holt-Winters combines the revenue level, trend and seasonal pattern. The prediction range expresses
        uncertainty; it does not guarantee the amount will be earned. The measure is recorded booking revenue, not
        profit, cash received or passenger numbers, and expenses are not forecast.
      </p>
      <p v-else class="details-note method-note">
        Holt-Winters combines the level, trend and seasonal pattern of the monthly {{ noun }}. The prediction range
        expresses uncertainty; it does not promise the number will be reached. The {{ noun }} are counted by booking
        date, not by travel date, and they are agency-wide, not per package.
      </p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { ForecastData } from '../api';
import { formatFull, formatMiss, formatShort, measureDescription, measureNoun, measureOf } from '../forecastUnits';
import UiIcon from './UiIcon.vue';

const props = defineProps<{ data: ForecastData }>();

const measure = computed(() => measureOf(props.data.measure));
const noun = computed(() => measureNoun(measure.value));

function full(value: number): string {
  return formatFull(value, measure.value);
}

function short(value: number): string {
  return formatShort(value, measure.value);
}

function miss(value: number): string {
  return formatMiss(value, measure.value);
}

interface BreakdownRow {
  step: number;
  monthLabel: string;
  value: number;
  lower: number;
  upper: number;
  isPeak: boolean;
  changePercent: number | null;
  changeDirection: 'up' | 'down' | 'flat';
}

const rows = computed<BreakdownRow[]>(() => {
  const steps = props.data.steps ?? [];
  if (!props.data.ok || steps.length === 0) return [];
  const history = props.data.history ?? [];
  const lastHistory = history.length > 0 ? history[history.length - 1] : null;
  const peak = Math.max(...steps.map((s) => s.value));

  return steps.map((s, idx) => {
    const prior = idx === 0
      ? (lastHistory && Number.isFinite(lastHistory.value) && lastHistory.value > 0 ? lastHistory.value : null)
      : steps[idx - 1].value;

    let changePercent: number | null = null;
    let changeDirection: BreakdownRow['changeDirection'] = 'flat';
    if (prior != null && prior > 0) {
      changePercent = ((s.value - prior) / prior) * 100;
      if (Math.abs(changePercent) >= 0.05) changeDirection = changePercent > 0 ? 'up' : 'down';
    }

    return {
      step: s.step,
      monthLabel: s.monthLabel,
      value: s.value,
      lower: s.lower,
      upper: s.upper,
      isPeak: Math.abs(s.value - peak) < 1e-4,
      changePercent,
      changeDirection,
    };
  });
});

const lastRecordedMonth = computed(() =>
  props.data.dataSource.lastRecordedMonth ?? 'the last recorded month');

function fixed(value: number | undefined): string {
  return value == null ? 'auto' : value.toFixed(3);
}

/** "August 2026" becomes "Aug 2026" for narrow screens. */
function shortMonth(label: string): string {
  const [name, year] = label.split(' ');
  return year ? `${name.slice(0, 3)} ${year}` : label;
}
</script>

<style scoped>
.forecast-details-body {
  display: grid;
  gap: 28px;
  padding-top: 4px;
}

.details-block {
  display: grid;
  gap: 12px;
  min-width: 0;
}

.details-heading {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

/* Six rows and five short columns fit any width, so the shared table minimum does not apply. */
.breakdown-table {
  min-width: 0;
}

.breakdown-table td {
  vertical-align: top;
}

.month-label {
  display: block;
  color: var(--color-text-primary);
  font-weight: 500;
}

.month-short {
  display: none;
}

.month-note {
  display: block;
  margin-top: 2px;
  color: var(--color-success-ink);
  font-size: var(--text-xs);
  font-weight: 600;
}

.narrow-range {
  display: none;
}

.change {
  font-weight: 600;
  white-space: nowrap;
}

.change-value {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
}

.change-mark {
  font-size: 10px;
}

.change.up {
  color: var(--color-success-ink);
}

.change.down {
  color: var(--color-danger-ink);
}

.change.flat {
  color: var(--color-text-muted);
}

.details-note {
  max-width: 75ch;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.model-grid {
  display: grid;
  gap: 24px;
}

.model-list {
  margin: 0;
}

.model-row {
  display: flex;
  justify-content: space-between;
  gap: 20px;
  padding: 9px 0;
  border-bottom: 1px solid var(--color-border-subtle);
  font-size: var(--text-base);
}

.model-row dt {
  color: var(--color-text-secondary);
}

.model-row dd {
  margin: 0;
  color: var(--color-text-primary);
  font-weight: 600;
  text-align: right;
  font-variant-numeric: lining-nums tabular-nums;
}

.fit-block {
  display: grid;
  gap: 4px;
  align-content: start;
}

.fit-heading {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.fit-block .details-note {
  margin-top: 8px;
}

.method-note {
  padding-top: 4px;
}

@media (min-width: 900px) {
  .model-grid {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: 40px;
  }
}

/* On phones the range moves under the forecast figure instead of taking two columns. */
@media (max-width: 640px) {
  .wide-only,
  .month-full {
    display: none;
  }

  .month-short {
    display: inline;
    white-space: nowrap;
  }

  .breakdown-table th,
  .breakdown-table td {
    padding: 10px 6px;
  }

  .breakdown-table th:first-child,
  .breakdown-table td:first-child {
    padding-left: 0;
  }

  .breakdown-table th:last-child,
  .breakdown-table td:last-child {
    padding-right: 0;
  }

  .change {
    font-size: var(--text-sm);
  }

  .narrow-range {
    display: block;
    margin-top: 2px;
    color: var(--color-text-muted);
    font-size: var(--text-xs);
    font-weight: 400;
  }

  .model-row {
    font-size: var(--text-sm);
  }
}
</style>
