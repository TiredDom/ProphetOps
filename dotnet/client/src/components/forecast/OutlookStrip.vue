<template>
  <ForecastBand
    v-if="visible"
    title="Demand outlook"
    :subtitle="`Holt-Winters forecast of passengers, by booking date, for ${forecastSpan(forecast)}`"
    :summary="summary"
  >
    <template #action>
      <RouterLink :to="{ path: '/home', query: { measure: 'passengers' } }" class="band-link">
        See the forecast <UiIcon name="arrow-right" />
      </RouterLink>
    </template>

    <div class="outlook">
      <div class="outlook-text">
        <p class="outlook-line">
          <template v-if="peak">
            Passenger numbers are forecast to peak in <strong class="band-model">{{ peak.monthLabel }}</strong>
            at <strong class="band-model">about {{ count(peak.value) }}</strong>.
          </template>
          <template v-else>Passenger numbers are forecast to stay level over the next six months.</template>
        </p>
        <p class="outlook-line">
          <strong class="band-figure">{{ count(openSlots) }}</strong> {{ openSlots === 1 ? 'slot is' : 'slots are' }} open across
          the catalog now<template v-if="peak && peak.value > openSlots">, fewer than the peak month's forecast</template>.
        </p>
        <p class="outlook-line">
          <template v-if="lowCount > 0">
            <strong class="band-figure">{{ lowCount }}</strong>
            {{ lowCount === 1 ? 'package is' : 'packages are' }} low on slots.
          </template>
          <template v-else>Every package has slots available.</template>
          <button
            v-if="lowCount > 0 && !lowActive"
            type="button"
            class="outlook-button"
            @click="emit('show-low')"
          >
            Show {{ lowCount === 1 ? 'it' : 'them' }}
          </button>
        </p>
        <p class="outlook-note band-muted">
          Open slots are today's total for every package. The forecast is for the whole agency, not each package,
          and counts passengers in the month they book.
        </p>
      </div>

      <ol class="outlook-months" :aria-label="`Forecast passengers by month, ${forecastSpan(forecast)}`">
        <li
          v-for="s in steps"
          :key="s.step"
          class="outlook-month"
          :class="{ peak: peak?.step === s.step, current: currentKey === monthKeyOfLabel(s.monthLabel) }"
        >
          <span class="outlook-value">{{ count(s.value) }}</span>
          <span class="outlook-bar-wrap" aria-hidden="true">
            <span class="outlook-bar" :style="{ height: barHeight(s.value) }"></span>
          </span>
          <span class="outlook-label">{{ shortLabel(s.monthLabel) }}</span>
        </li>
      </ol>
    </div>
  </ForecastBand>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useBusinessDate } from '../../composables/useBusinessDate';
import { useForecast } from '../../composables/useForecast';
import { forecastSpan, monthKeyOfLabel, peakStep, usableSteps } from '../../forecastFacts';
import { count } from '../../format';
import UiIcon from '../UiIcon.vue';
import ForecastBand from './ForecastBand.vue';

const props = defineProps<{ lowCount: number; lowActive: boolean; openSlots: number }>();
const emit = defineEmits<{ 'show-low': [] }>();

// Packages are about people and seats, so this band reads the passenger forecast.
const { state, allowed, load } = useForecast('passengers');
const { today, load: loadToday } = useBusinessDate();

const forecast = computed(() => state.data);
const steps = computed(() => usableSteps(forecast.value));
const peak = computed(() => peakStep(forecast.value));
const visible = computed(() => allowed.value && steps.value.length > 0);
const currentKey = computed(() => today.value.slice(0, 7));
const top = computed(() => Math.max(...steps.value.map((s) => s.value), 1));

const summary = computed(() => {
  const open = `${count(props.openSlots)} slots open`;
  return peak.value ? `Peak ${peak.value.monthLabel}, about ${count(peak.value.value)} passengers · ${open}` : open;
});

function barHeight(value: number): string {
  return `${Math.max(6, Math.round((value / top.value) * 100))}%`;
}

/** "December 2026" becomes "Dec"; January also carries its year so the turn of the year shows. */
function shortLabel(label: string): string {
  const [month, year] = label.split(' ');
  const short = month.slice(0, 3);
  return month === 'January' && year ? `${short} ${year.slice(2)}` : short;
}

onMounted(() => {
  void load();
  void loadToday();
});
</script>

<style scoped>
.outlook {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px 32px;
}

.outlook-text {
  display: grid;
  gap: 6px;
  flex: 1 1 320px;
  min-width: 0;
  color: var(--color-on-shell-soft);
  font-size: var(--text-base);
}

.outlook-line strong {
  font-size: var(--text-md);
}

.outlook-button {
  margin-left: 8px;
  padding: 3px 12px;
  border: 1px solid rgba(134, 217, 197, 0.55);
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-model-on-shell);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.outlook-button:hover {
  background: rgba(134, 217, 197, 0.12);
}

.outlook-button:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

.outlook-note {
  max-width: 62ch;
  font-size: var(--text-xs);
  line-height: 1.5;
}

/* Six forecast months as columns; the peak is solid teal, this month is outlined. */
.outlook-months {
  display: grid;
  grid-template-columns: repeat(6, minmax(44px, 1fr));
  gap: 8px;
  flex: 0 1 420px;
  min-width: 0;
  margin: 0;
  padding: 0;
  list-style: none;
}

.outlook-month {
  display: grid;
  grid-template-rows: auto 64px auto;
  justify-items: center;
  gap: 4px;
  min-width: 0;
}

.outlook-value {
  color: var(--color-on-shell-muted);
  font-size: var(--text-xs);
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.outlook-bar-wrap {
  display: flex;
  align-items: flex-end;
  width: 100%;
  max-width: 34px;
  height: 64px;
}

.outlook-bar {
  width: 100%;
  border-radius: 3px 3px 0 0;
  background: rgba(134, 217, 197, 0.35);
}

.outlook-month.peak .outlook-bar {
  background: var(--color-model-on-shell);
}

.outlook-month.peak .outlook-value {
  color: var(--color-model-on-shell);
  font-weight: 650;
}

.outlook-month.current .outlook-bar {
  box-shadow: inset 0 0 0 2px var(--color-on-shell);
}

.outlook-label {
  color: var(--color-on-shell-soft);
  font-size: var(--text-xs);
  white-space: nowrap;
}

.outlook-month.current .outlook-label {
  color: var(--color-on-shell);
  font-weight: 650;
}

@media (max-width: 640px) {
  .outlook-months {
    flex-basis: 100%;
    gap: 4px;
  }

  .outlook-month {
    grid-template-rows: auto 48px auto;
  }

  .outlook-bar-wrap {
    height: 48px;
  }
}
</style>
