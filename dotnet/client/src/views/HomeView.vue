<template>
  <AppShell title="Home">
    <div class="home-page">
      <div v-if="loading" class="home-page" aria-busy="true" aria-label="Loading home">
        <section class="content-panel">
          <div class="placeholder-block chart-placeholder"></div>
        </section>
        <section class="content-panel">
          <Skeleton :rows="2" />
        </section>
        <div class="home-columns">
          <section class="content-panel"><Skeleton :rows="4" /></section>
          <section class="content-panel"><Skeleton :rows="4" /></section>
        </div>
      </div>

      <section v-else-if="error" class="home-alert" role="alert">
        <div>
          <h2 class="home-alert-title">Home could not be loaded</h2>
          <p class="home-alert-text">{{ error }}</p>
        </div>
        <button type="button" class="secondary-button" @click="load">Try again</button>
      </section>

      <template v-else-if="data">
        <!-- 1. The forecast and the actions it suggests -->
        <ForecastOverviewCard
          v-if="forecast?.ok"
          :key="measure"
          :data="forecast"
          :measure="measure"
          :show-recommendations="true"
          details-target="forecast-details"
          :title="forecastTitle(measure)"
          :subtitle="forecastBasis"
          :decision-link="decisionLink"
          @details="openDetails"
        >
          <template #controls>
            <div class="measure-switch" role="group" aria-label="What to forecast">
              <button
                v-for="option in measures"
                :key="option.key"
                type="button"
                class="measure-option"
                :aria-pressed="measure === option.key"
                @click="chooseMeasure(option.key)"
              >
                {{ option.label }}
              </button>
            </div>
          </template>
          <template #lead>
            <p class="forecast-headline"><template v-for="(part, index) in headline" :key="index"><strong v-if="part.strong">{{ part.text }}</strong><template v-else>{{ part.text }}</template></template></p>
            <p class="forecast-subline">
              <template v-if="measure === 'revenue'">
                About <strong>{{ formatConcisePeso(sixMonthTotal) }}</strong> in booking revenue across {{ forecastMonths }},
                each month with an 80% range.
              </template>
              <template v-else>
                About <strong>{{ withUnit(sixMonthTotal, measure) }}</strong> across {{ forecastMonths }}, counted by
                booking date, each month with an 80% range.
              </template>
            </p>
          </template>
          <template v-if="gap" #notice>
            <div class="forecast-gap" role="note">
              <UiIcon name="alert" class="forecast-gap-icon" />
              <p class="forecast-gap-text">
                No bookings are recorded for {{ describeMonths(gap) }}, so the forecast works from
                {{ gap.lastRecorded }}. Record those months to bring it up to date.
              </p>
              <RouterLink to="/bookings" class="forecast-gap-link">Open bookings <UiIcon name="arrow-right" /></RouterLink>
            </div>
          </template>
        </ForecastOverviewCard>

        <section
          v-else-if="forecast && !forecast.ok"
          class="content-panel forecast-unavailable"
          aria-labelledby="forecast-unavailable-title"
        >
          <div class="unavailable-head">
            <h2 id="forecast-unavailable-title" class="panel-heading">Forecast unavailable</h2>
            <span class="unavailable-count">{{ forecast.dataSource.recordedMonths }} of {{ forecast.dataSource.minimumMonths }} months recorded</span>
          </div>
          <p class="unavailable-text">
            Holt-Winters needs two full years of monthly booking records to tell the seasonal pattern apart from the
            trend.
            <template v-if="monthsNeeded > 0">
              {{ monthsNeeded }} more {{ monthsNeeded === 1 ? 'month is' : 'months are' }} needed before the forecast can start.
            </template>
          </p>
          <p class="unavailable-note">The forecast covers booking revenue in pesos, not passenger numbers, profit or cash received.</p>
          <button v-if="measure !== 'revenue'" type="button" class="secondary-button" @click="chooseMeasure('revenue')">
            Show the revenue forecast
          </button>
          <RouterLink to="/bookings" class="text-link">View booking records <UiIcon name="arrow-right" /></RouterLink>
        </section>

        <section v-else-if="forecastLoading" class="content-panel" aria-busy="true" aria-label="Loading the forecast">
          <div class="placeholder-block chart-placeholder"></div>
        </section>

        <section v-else-if="forecastError" class="content-panel forecast-unavailable" aria-labelledby="forecast-error-title">
          <div class="unavailable-head">
            <h2 id="forecast-error-title" class="panel-heading">Forecast could not be loaded</h2>
          </div>
          <p class="unavailable-text">{{ forecastError }}</p>
          <div>
            <button type="button" class="secondary-button" @click="loadForecast">Try again</button>
          </div>
        </section>

        <!-- 2. The numbers behind the forecast: closed by default, with a preview of what is inside -->
        <details
          v-if="forecast?.ok"
          id="forecast-details"
          ref="detailsEl"
          class="content-panel forecast-details"
          :open="detailsOpen"
          @toggle="onDetailsToggle"
        >
          <summary class="details-summary">
            <span class="details-summary-text">
              <span class="panel-heading">Monthly figures and model details</span>
              <span class="details-summary-meta">{{ detailsRange }}</span>
            </span>
            <span class="details-preview">
              <span v-if="peak" class="details-chip">
                <span class="details-chip-label">Highest</span> {{ peak.month }} ·
                {{ measure === 'revenue' ? pesoCompact(peak.value) : withUnit(peak.value, measure) }}
              </span>
              <span v-if="forecast.metrics?.mape != null" class="details-chip">
                <span class="details-chip-label">Average miss</span> {{ forecast.metrics.mape.toFixed(1) }}%
              </span>
            </span>
            <UiIcon name="chevron-down" class="details-chevron" />
          </summary>
          <ForecastDetails :data="forecast" />
        </details>

        <!-- 3. This month so far against the same days last month -->
        <MonthScoreboard v-if="data.thisMonth" :month="data.thisMonth" />

        <div class="home-columns">
          <!-- 4. What needs someone's attention now -->
          <AttentionInbox :unpaid="data.pendingPayments" :low="data.lowStockPackages" />

          <!-- 5. Who changed what; refreshes on its own and tells Home when something changed -->
          <ActivityFeed :limit="6" @changed="refreshDashboard" />
        </div>
      </template>
    </div>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import ForecastDetails from '../components/ForecastDetails.vue';
import ForecastOverviewCard from '../components/ForecastOverviewCard.vue';
import Skeleton from '../components/Skeleton.vue';
import UiIcon from '../components/UiIcon.vue';
import ActivityFeed from '../components/home/ActivityFeed.vue';
import AttentionInbox from '../components/home/AttentionInbox.vue';
import MonthScoreboard from '../components/home/MonthScoreboard.vue';
import { api, describeError, type DashboardData } from '../api';
import { pesoCompact } from '../format';
import { formatConcisePeso, type DecisionRecommendation } from '../forecastChart';
import {
  describeMonths,
  forecastHeadline,
  forecastSpan,
  forecastTotal,
  monthKeyOfLabel,
  sameMonthLastYear,
  unrecordedMonths,
} from '../forecastFacts';
import { useForecast } from '../composables/useForecast';
import { useQueryState } from '../composables/useQueryState';
import type { ForecastMeasure } from '../api';
import { forecastTitle, isMeasure, measureNoun, measureOf, measures, withUnit } from '../forecastUnits';

const DETAILS_HASH = '#forecast-details';

const route = useRoute();
const router = useRouter();

const data = ref<DashboardData | null>(null);

// Revenue is the study's main forecast; bookings and passengers use the same method on counts.
const view = useQueryState({ measure: 'revenue' }, { measure: isMeasure });
const measure = computed<ForecastMeasure>(() => measureOf(view.measure));
const stores = {
  revenue: useForecast('revenue'),
  bookings: useForecast('bookings'),
  passengers: useForecast('passengers'),
};
const forecastStore = computed(() => stores[measure.value]);
const forecast = computed(() => forecastStore.value.state.data);
const forecastLoading = computed(() => forecastStore.value.state.loading && !forecast.value);
const loading = ref(true);
const error = ref('');
const forecastError = ref('');
const detailsOpen = ref(false);
const detailsEl = ref<HTMLDetailsElement | null>(null);

const monthsNeeded = computed(() => {
  const source = forecast.value?.dataSource;
  return source ? Math.max(0, source.minimumMonths - source.recordedMonths) : 0;
});

const detailsRange = computed(() => {
  const steps = forecast.value?.steps ?? [];
  return steps.length ? `${steps[0].monthLabel} – ${steps[steps.length - 1].monthLabel}` : '';
});

// Home reads the business date off the month-so-far figures, which end today.
const today = computed(() => data.value?.thisMonth?.current.to ?? '');

const headline = computed(() => forecastHeadline(forecast.value));
const sixMonthTotal = computed(() => forecastTotal(forecast.value));
const forecastMonths = computed(() => forecastSpan(forecast.value));
const gap = computed(() => unrecordedMonths(forecast.value, today.value));

const forecastBasis = computed(() => {
  const source = forecast.value?.dataSource;
  if (!source) return '';
  if (source.usingSample) return 'Holt-Winters · 6 months ahead · sample series, not recorded bookings';
  const until = source.lastRecordedMonth ? ` to ${source.lastRecordedMonth}` : '';
  const what = measure.value === 'revenue' ? '' : ` · ${measureNoun(measure.value)} by booking date`;
  return `Holt-Winters${what} · 6 months ahead · learned from ${source.liveMonthsAvailable} months of bookings${until}`;
});

/**
 * Each decision opens the records for the same month last year, the season the forecast leans on:
 * spending for revenue, bookings for the counts. A demand peak opens the packages low on slots.
 */
function decisionLink(rec: DecisionRecommendation) {
  const steps = forecast.value?.steps ?? [];
  const stepNumber = rec.stepNumbers[rec.stepNumbers.length - 1];
  const step = steps.find((s) => s.step === stepNumber);
  const key = step ? monthKeyOfLabel(step.monthLabel) : null;
  const counting = measure.value !== 'revenue';
  if (counting && rec.id === 'peak') {
    return { to: { path: '/packages', query: { stock: 'low' } }, label: 'See packages low on slots' };
  }
  if (!key) return counting ? { to: '/bookings', label: 'Review bookings' } : { to: '/expenses', label: 'Review spending' };
  const last = sameMonthLastYear(key);
  const query = { period: 'custom', from: last.from, to: last.to };
  return counting
    ? { to: { path: '/bookings', query }, label: `See bookings in ${last.label}` }
    : { to: { path: '/expenses', query }, label: `See spending in ${last.label}` };
}

async function chooseMeasure(next: ForecastMeasure) {
  view.measure = next;
  forecastError.value = '';
  const result = await stores[next].load();
  if (!result) forecastError.value = stores[next].state.error || 'The forecast could not be loaded.';
}

const peak = computed(() => {
  const steps = forecast.value?.steps ?? [];
  if (steps.length === 0) return null;
  const top = steps.reduce((best, step) => (step.value > best.value ? step : best), steps[0]);
  return { month: top.monthLabel.split(' ')[0], value: top.value };
});

function onDetailsToggle(event: Event) {
  detailsOpen.value = (event.target as HTMLDetailsElement).open;
}

async function openDetails() {
  detailsOpen.value = true;
  await nextTick();
  const el = detailsEl.value;
  if (!el) return;
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  el.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
  el.querySelector<HTMLElement>('summary')?.focus({ preventScroll: true });
  if (route.hash !== DETAILS_HASH) await router.replace({ hash: DETAILS_HASH });
}

async function loadForecast() {
  forecastError.value = '';
  // Home always asks for a fresh forecast; the other pages reuse it.
  const result = await forecastStore.value.load(true);
  if (!result) forecastError.value = forecastStore.value.state.error || 'The forecast could not be loaded.';
}

/** A change came through the activity feed; bring the counts up to date without a loading state. */
async function refreshDashboard() {
  try {
    data.value = await api.dashboard();
  } catch {
    // The figures on screen stay as they were; the next change will try again.
  }
}

async function load() {
  loading.value = true;
  error.value = '';
  try {
    const [dashboard] = await Promise.all([api.dashboard(), loadForecast()]);
    data.value = dashboard;
  } catch (err: unknown) {
    error.value = describeError(err, 'Check the connection, then try again.');
  } finally {
    loading.value = false;
  }
  if (route.hash === DETAILS_HASH && forecast.value?.ok) await openDetails();
}

watch(() => route.hash, (hash) => {
  if (hash === DETAILS_HASH && !detailsOpen.value && forecast.value?.ok) void openDetails();
});

onMounted(load);
</script>

<style scoped>
.home-page {
  display: grid;
  gap: 20px;
}

.panel-heading {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

/* The measure switch sits on the navy band, beside the title. */
.measure-switch {
  display: inline-flex;
  gap: 2px;
  padding: 3px;
  border: 1px solid rgba(255, 255, 255, 0.18);
  border-radius: var(--radius-pill);
}

.measure-option {
  min-height: 34px;
  padding: 0 14px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-on-shell-soft);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.measure-option:hover {
  color: var(--color-on-shell);
  background: rgba(255, 255, 255, 0.08);
}

.measure-option[aria-pressed='true'] {
  background: var(--color-model-on-shell);
  color: var(--color-shell);
}

.measure-option:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

/* What the forecast says, in a sentence, on the navy band: the figures carry the model's teal. */
.forecast-headline {
  max-width: 44ch;
  color: var(--color-on-shell);
  font-size: 26px;
  font-weight: 600;
  letter-spacing: -0.016em;
  line-height: 1.3;
  text-wrap: pretty;
}

.forecast-headline strong {
  color: var(--color-model-on-shell);
  font-weight: 700;
  white-space: nowrap;
}

.forecast-subline {
  margin-top: 8px;
  color: var(--color-on-shell-muted);
  font-size: var(--text-base);
}

.forecast-subline strong {
  color: var(--color-on-shell);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.forecast-gap {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 6px 10px;
  padding: 10px 24px;
  border-bottom: 1px solid var(--tone-warning-border);
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.forecast-gap-icon {
  margin-top: 2px;
  font-size: 17px;
}

.forecast-gap-text {
  flex: 1 1 320px;
  min-width: 0;
}

.forecast-gap-link {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--tone-warning-ink);
  font-weight: 650;
  text-decoration: underline;
  text-underline-offset: 3px;
  white-space: nowrap;
}

@media (max-width: 760px) {
  .forecast-gap {
    padding: 10px 14px;
  }

  .forecast-headline {
    font-size: 20px;
  }

  .measure-switch {
    display: flex;
    width: 100%;
  }

  .measure-option {
    flex: 1 1 0;
    min-height: 40px;
    padding: 0 8px;
  }

  .forecast-subline {
    font-size: var(--text-sm);
  }
}

.text-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  color: var(--color-primary);
  font-size: var(--text-sm);
  font-weight: 600;
  text-decoration: none;
}

.text-link:hover {
  color: var(--color-primary-hover);
  text-decoration: underline;
  text-underline-offset: 3px;
}

/* Forecast unavailable or failed */
.forecast-unavailable {
  display: grid;
  gap: 10px;
  justify-items: start;
}

.unavailable-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 16px;
}

.unavailable-count {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.unavailable-text {
  max-width: 70ch;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.55;
}

.unavailable-note {
  max-width: 70ch;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

/* Forecast details: a drawer of figures that says what it holds before it opens */
.forecast-details {
  padding-top: 0;
  padding-bottom: 0;
}

.forecast-details[open] {
  padding-bottom: 24px;
}

.details-summary {
  display: flex;
  align-items: center;
  gap: 14px;
  min-height: 64px;
  margin: 0 -24px;
  padding: 12px 24px;
  border-radius: var(--radius-lg);
  cursor: pointer;
  list-style: none;
  transition: background var(--transition-fast);
}

.details-summary::-webkit-details-marker {
  display: none;
}

.details-summary:hover {
  background: var(--color-surface-hover);
}

.details-summary:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: -2px;
}

.forecast-details[open] .details-summary {
  margin-bottom: 8px;
  border-bottom: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-lg) var(--radius-lg) 0 0;
}

.details-summary-text {
  display: grid;
  flex: 1 1 0;
  gap: 1px;
  min-width: 0;
}

.details-summary-meta {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.details-preview {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
}

.details-chip {
  padding: 3px 10px;
  border-radius: var(--radius-pill);
  background: var(--tone-neutral-surface);
  color: var(--color-text-primary);
  font-size: var(--text-xs);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.details-chip-label {
  color: var(--color-text-muted);
  font-weight: 500;
}

.details-chevron {
  color: var(--color-text-muted);
  font-size: 20px;
  transition: transform var(--transition-normal);
}

.forecast-details[open] .details-chevron {
  transform: rotate(180deg);
}

/* Needs attention and recent activity sit side by side on wide screens */
.home-columns {
  display: grid;
  gap: 20px;
  align-items: start;
}

/* Load failure */
.home-alert {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 20px;
  background: var(--tone-danger-surface);
  border: 1px solid var(--tone-danger-border);
  border-radius: var(--radius-lg);
}

.home-alert-title {
  color: var(--tone-danger-ink);
  font-size: var(--text-base);
  font-weight: 600;
}

.home-alert-text {
  margin-top: 2px;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

/* Loading */
.placeholder-block {
  border-radius: var(--radius-md);
  background: linear-gradient(90deg, var(--color-surface-sunken) 0%, var(--color-border-subtle) 50%, var(--color-surface-sunken) 100%);
  background-size: 200% 100%;
  animation: placeholder-shimmer 1.4s ease-in-out infinite;
}

.chart-placeholder {
  height: 320px;
}

@keyframes placeholder-shimmer {
  0% {
    background-position: 200% 0;
  }

  100% {
    background-position: -200% 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .placeholder-block {
    animation: none;
    background: var(--color-surface-sunken);
  }
}

@media (min-width: 1100px) {
  .home-columns {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}

@media (max-width: 760px) {
  .details-summary {
    flex-wrap: wrap;
    margin: 0 -16px;
    padding: 12px 16px;
  }

  .details-preview {
    order: 3;
    flex-basis: 100%;
    justify-content: flex-start;
    padding-left: 0;
  }

  .chart-placeholder {
    height: 260px;
  }
}
</style>
