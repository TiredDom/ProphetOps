<template>
  <ForecastBand
    v-if="visible"
    :title="`${monthLabel} against the forecast`"
    subtitle="Bookings recorded so far, beside what Holt-Winters expects for the whole month"
    :summary="summary"
  >
    <template #action>
      <RouterLink to="/home" class="band-link">See the forecast <UiIcon name="arrow-right" /></RouterLink>
    </template>

    <template v-if="step">
      <p class="pace-figures">
        <span><strong class="band-figure">{{ peso(recorded) }}</strong> recorded so far</span>
        <span>
          <strong class="band-model">{{ formatConcisePeso(step.value) }}</strong> forecast for {{ monthName(step) }},
          <span class="band-muted">likely {{ formatConcisePeso(step.lower) }} to {{ formatConcisePeso(step.upper) }}</span>
        </span>
        <span class="band-muted">Day {{ day }} of {{ daysInMonth }}</span>
      </p>
      <p v-if="bookingStep" class="pace-figures pace-counts">
        <span>
          <strong class="band-figure">{{ count(recordedBookings) }}</strong>
          {{ recordedBookings === 1 ? 'booking' : 'bookings' }} so far
        </span>
        <span>
          <strong class="band-model">about {{ count(bookingStep.value) }}</strong> forecast,
          <span class="band-muted">likely {{ count(bookingStep.lower) }} to {{ count(bookingStep.upper) }}</span>
        </span>
      </p>

      <div class="pace-track" role="img" :aria-label="trackLabel">
        <span class="pace-range" :style="{ left: percent(step.lower), width: span(step.lower, step.upper) }"></span>
        <span class="pace-recorded" :style="{ width: percent(recorded) }"></span>
        <span class="pace-marker" :style="{ left: percent(step.value) }"></span>
      </div>
      <div class="pace-key" aria-hidden="true">
        <span><i class="key-recorded"></i>Recorded</span>
        <span><i class="key-range"></i>Likely range</span>
        <span><i class="key-marker"></i>Forecast</span>
      </div>
    </template>

    <p v-else class="band-muted">
      The forecast covers {{ forecastSpan(forecast) }}, so it says nothing about {{ monthLabel }} yet.
    </p>

    <p v-if="gap" class="band-warning">
      <UiIcon name="alert" />
      <span>
        No bookings are recorded for {{ describeMonths(gap) }}, so the forecast works from {{ gap.lastRecorded }}.
        Recording them brings it up to date.
      </span>
    </p>
  </ForecastBand>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useForecast } from '../../composables/useForecast';
import { formatConcisePeso } from '../../forecastChart';
import {
  describeMonths,
  forecastSpan,
  labelOfMonthKey,
  monthName,
  stepForMonth,
  unrecordedMonths,
} from '../../forecastFacts';
import { count, peso } from '../../format';
import UiIcon from '../UiIcon.vue';
import ForecastBand from './ForecastBand.vue';

// `recorded` is this month's booking revenue so far and `recordedBookings` the number of bookings,
// voided bookings left out of both.
const props = withDefaults(defineProps<{ recorded: number; today: string; recordedBookings?: number }>(), {
  recordedBookings: 0,
});

const { state, allowed, load } = useForecast();
const bookingForecast = useForecast('bookings');
const forecast = computed(() => state.data);

const monthKey = computed(() => props.today.slice(0, 7));
const monthLabel = computed(() => (monthKey.value ? labelOfMonthKey(monthKey.value) : 'This month'));
const step = computed(() => stepForMonth(forecast.value, monthKey.value));
const bookingStep = computed(() => stepForMonth(bookingForecast.state.data, monthKey.value));
const gap = computed(() => unrecordedMonths(forecast.value, props.today));
const visible = computed(() => allowed.value && !!forecast.value?.ok && !!props.today);

const day = computed(() => Number(props.today.slice(8, 10)) || 1);
const daysInMonth = computed(() => {
  const [year, month] = monthKey.value.split('-').map(Number);
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
});

const scale = computed(() => Math.max(step.value?.upper ?? 0, step.value?.value ?? 0, props.recorded, 1) * 1.06);

function percent(value: number): string {
  return `${Math.max(0, Math.min(100, (value / scale.value) * 100))}%`;
}

function span(from: number, to: number): string {
  return `${Math.max(0, Math.min(100, ((to - from) / scale.value) * 100))}%`;
}

const summary = computed(() => {
  if (!step.value) return 'The forecast does not cover this month yet.';
  const counted = bookingStep.value ? ` · ${count(props.recordedBookings)} of about ${count(bookingStep.value.value)} bookings` : '';
  const recorded = `${peso(props.recorded)} of ${formatConcisePeso(step.value.value)} forecast${counted}`;
  return gap.value ? `${recorded} · recent months unrecorded` : recorded;
});

const trackLabel = computed(() => {
  if (!step.value) return '';
  return `${peso(props.recorded)} recorded so far against a forecast of ${formatConcisePeso(step.value.value)}, `
    + `likely between ${formatConcisePeso(step.value.lower)} and ${formatConcisePeso(step.value.upper)}.`;
});

onMounted(() => {
  void load();
  void bookingForecast.load();
});
</script>

<style scoped>
.pace-figures {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 22px;
  color: var(--color-on-shell-soft);
  font-size: var(--text-base);
}

.pace-figures strong {
  font-size: var(--text-lg);
  letter-spacing: -0.01em;
}

.pace-counts {
  margin-top: -4px;
}

.pace-counts strong {
  font-size: var(--text-md);
}

/* Recorded revenue fills from the left in white; the likely range is a teal band with the
   forecast itself marked inside it. */
.pace-track {
  position: relative;
  height: 14px;
  border-radius: var(--radius-pill);
  background: rgba(255, 255, 255, 0.08);
  overflow: hidden;
}

.pace-range {
  position: absolute;
  top: 0;
  bottom: 0;
  background: rgba(134, 217, 197, 0.3);
}

.pace-recorded {
  position: absolute;
  top: 3px;
  bottom: 3px;
  left: 0;
  min-width: 3px;
  border-radius: var(--radius-pill);
  background: var(--color-on-shell);
}

.pace-marker {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 3px;
  margin-left: -1.5px;
  background: var(--color-model-on-shell);
}

.pace-key {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 18px;
  color: var(--color-on-shell-muted);
  font-size: var(--text-xs);
}

.pace-key span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.pace-key i {
  display: inline-block;
  width: 14px;
  height: 8px;
  border-radius: 2px;
}

.key-recorded {
  background: var(--color-on-shell);
}

.key-range {
  background: rgba(134, 217, 197, 0.45);
}

.pace-key i.key-marker {
  width: 3px;
  height: 12px;
  background: var(--color-model-on-shell);
}
</style>
