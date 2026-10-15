<template>
  <section class="details-block accuracy-block" aria-labelledby="accuracy-title" :aria-busy="loading && !test">
    <div class="accuracy-head">
      <h3 id="accuracy-title" class="accuracy-heading">
        <UiIcon name="forecast" class="accuracy-mark" />
        Accuracy on months it had not seen
      </h3>
      <a v-if="test?.ok" class="accuracy-download" :href="csvUrl" download>
        <UiIcon name="download" />
        Download the test as CSV
      </a>
    </div>

    <Skeleton v-if="loading && !test" :rows="3" />

    <div v-else-if="error && !test" class="accuracy-problem" role="alert">
      <p>{{ error }}</p>
      <button type="button" class="secondary-button" @click="retry">Try again</button>
    </div>

    <p v-else-if="test && !test.ok" class="accuracy-lead">
      The test needs {{ test.monthsRequired }} recorded months: two years to learn from and at least one more to
      check. {{ test.monthsAvailable }} {{ test.monthsAvailable === 1 ? 'is' : 'are' }} recorded so far.
    </p>

    <template v-else-if="test?.ok && overall">
      <div class="accuracy-lead">
        <p>
          The forecast was rerun from each of {{ test.origins }} past {{ test.origins === 1 ? 'month' : 'months' }},
          using only the months before it, and checked against what was then recorded from
          {{ test.firstTestedMonth }} to {{ test.lastTestedMonth }}: {{ overall.forecasts }} forecasts in all.
        </p>
        <p>
          <template v-if="overall.mape != null">
            It missed by <strong class="model-figure">{{ percent(overall.mape) }}</strong> on average
            (about {{ miss(overall.mae) }} a month), against <strong>{{ percent(overall.seasonalNaiveMape) }}</strong>
            for repeating the same month last year.
          </template>
          <template v-else>
            It missed by about <strong class="model-figure">{{ miss(overall.mae) }}</strong> a month on average,
            against {{ miss(overall.seasonalNaiveMae) }} for repeating the same month last year.
          </template>
          {{ comparison }}
        </p>
        <p>
          The recorded figure fell inside the 80% range in {{ overall.insideRange }} of {{ overall.forecasts }} forecasts
          ({{ share(overall.coverage) }}). The range aims for about 8 in 10.
        </p>
      </div>

      <div class="table-scroll">
        <table class="dss-table accuracy-table">
          <caption class="sr-only">Average miss by how far ahead the forecast was made</caption>
          <thead>
            <tr>
              <th scope="col">Months ahead</th>
              <th scope="col" class="num">
                <span class="label-full">Holt-Winters miss</span>
                <span class="label-short">Forecast</span>
              </th>
              <th scope="col" class="num">
                <span class="label-full">Same month last year</span>
                <span class="label-short">Last year</span>
              </th>
              <th scope="col" class="num">
                <span class="label-full">Inside the range</span>
                <span class="label-short">In range</span>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in test.byHorizon" :key="row.step">
              <th scope="row">{{ row.step }} {{ row.step === 1 ? 'month' : 'months' }}</th>
              <td class="num model-figure" :class="{ smaller: smaller(row.mape, row.seasonalNaiveMape) }">
                {{ percent(row.mape) }}
              </td>
              <td class="num" :class="{ smaller: smaller(row.seasonalNaiveMape, row.mape) }">
                {{ percent(row.seasonalNaiveMape) }}
              </td>
              <td class="num">{{ row.insideRange }} of {{ row.forecasts }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <p class="accuracy-note">
        A miss is the average difference from the recorded figure, as a share of it; months with nothing recorded
        are left out. The test reads the months recorded in ProphetOps and shows how the method did on them. It is
        not a promise about the months ahead.
        <template v-if="test.dataSource.usingSample"> These are sample months, not recorded bookings.</template>
      </p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue';
import type { ForecastMeasure } from '../../api';
import { useForecastEvaluation } from '../../composables/useForecast';
import { formatMiss } from '../../forecastUnits';
import Skeleton from '../Skeleton.vue';
import UiIcon from '../UiIcon.vue';

const props = defineProps<{ measure: ForecastMeasure; active: boolean }>();

const evaluation = useForecastEvaluation(() => props.measure);
const test = computed(() => evaluation.state.value.data);
const loading = computed(() => evaluation.state.value.loading);
const error = computed(() => evaluation.state.value.error);
const overall = computed(() => test.value?.overall ?? null);

const csvUrl = computed(() => `/api/export/forecast-test.csv?measure=${props.measure}`);

// The test reruns the forecast from every past month, so it waits until the details are open.
watch(
  () => [props.active, props.measure] as const,
  ([active]) => {
    if (active) void evaluation.load();
  },
  { immediate: true },
);

function retry() {
  void evaluation.load(true);
}

const comparison = computed(() => {
  const skill = overall.value?.skillVsSeasonalNaive;
  if (skill == null) return '';
  if (skill >= 0.005) return `That is a ${Math.round(skill * 100)}% smaller miss.`;
  if (skill <= -0.005) return 'On these months, repeating the same month last year did better.';
  return 'The two did about the same.';
});

function percent(value: number | null | undefined): string {
  return value == null ? 'Not available' : `${value.toFixed(1)}%`;
}

function share(coverage: number): string {
  return `${Math.round(coverage * 100)}%`;
}

function miss(value: number): string {
  return formatMiss(value, props.measure);
}

/** The smaller of two misses reads in a heavier weight; colour stays out of it. */
function smaller(value: number | null, other: number | null): boolean {
  return value != null && other != null && value < other;
}
</script>

<style scoped>
.accuracy-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2) var(--space-5);
}

.accuracy-heading {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.accuracy-mark {
  color: var(--color-model);
  font-size: 18px;
}

.accuracy-download {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  color: var(--color-primary);
  font-size: var(--text-sm);
  font-weight: 600;
  text-decoration: none;
}

.accuracy-download:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.accuracy-download:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-radius: var(--radius-sm);
}

.accuracy-lead {
  display: grid;
  gap: var(--space-2);
  max-width: 75ch;
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.55;
}

.accuracy-lead p {
  margin: 0;
}

.accuracy-lead strong {
  color: var(--color-text-primary);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.accuracy-lead strong.model-figure {
  color: var(--color-model-ink);
}

.accuracy-problem {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3) var(--space-4);
  color: var(--color-text-secondary);
  font-size: var(--text-base);
}

.accuracy-problem p {
  margin: 0;
}

/* Four short columns fit any width, so the shared table minimum does not apply. */
.accuracy-table {
  min-width: 0;
}

.accuracy-table tbody th {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 500;
  border-bottom: 1px solid var(--color-border-subtle);
}

.accuracy-table td.model-figure {
  color: var(--color-model-ink);
}

.accuracy-table td.smaller {
  font-weight: 600;
}

.label-short {
  display: none;
}

.accuracy-note {
  max-width: 75ch;
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

@media (max-width: 640px) {
  .label-full {
    display: none;
  }

  .label-short {
    display: inline;
  }

  .accuracy-table th,
  .accuracy-table td {
    padding: 10px 6px;
  }

  .accuracy-table th:first-child {
    padding-left: 0;
  }

  .accuracy-table th:last-child,
  .accuracy-table td:last-child {
    padding-right: 0;
  }

  .accuracy-lead {
    font-size: var(--text-sm);
  }
}
</style>
