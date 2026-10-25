<template>
  <RouterLink v-if="visible && step" to="/home" class="forecast-pulse" :aria-label="label">
    <span class="pulse-head">
      <UiIcon name="forecast" :stroke-width="2" class="pulse-icon" />
      Forecast
    </span>
    <span class="pulse-month">{{ isCurrent ? 'This month' : step.monthLabel }}</span>
    <span class="pulse-value">{{ formatConcisePeso(step.value) }}</span>
    <span class="pulse-range">likely {{ formatConcisePeso(step.lower) }} to {{ formatConcisePeso(step.upper) }}</span>
    <span v-if="peak && peak.step !== step.step" class="pulse-peak">
      Peak {{ peak.monthLabel }} · {{ formatConcisePeso(peak.value) }}
    </span>
  </RouterLink>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useBusinessDate } from '../../composables/useBusinessDate';
import { useForecast } from '../../composables/useForecast';
import { formatConcisePeso } from '../../forecastChart';
import { peakStep, stepForMonth, usableSteps } from '../../forecastFacts';
import UiIcon from '../UiIcon.vue';

// The forecast stays in view on every page: this month's figure when the forecast covers it,
// otherwise its first month.
const { state, allowed, load } = useForecast();
const { today, load: loadToday } = useBusinessDate();

const current = computed(() => (today.value ? stepForMonth(state.data, today.value.slice(0, 7)) : null));
const step = computed(() => current.value ?? usableSteps(state.data)[0] ?? null);
const isCurrent = computed(() => current.value !== null);
const peak = computed(() => peakStep(state.data));
const visible = computed(() => allowed.value && !!state.data?.ok);

const label = computed(() => {
  if (!step.value) return 'Forecast';
  const month = isCurrent.value ? 'this month' : step.value.monthLabel;
  return `Forecast for ${month}: ${formatConcisePeso(step.value.value)}, likely ${formatConcisePeso(step.value.lower)} `
    + `to ${formatConcisePeso(step.value.upper)}. Open the forecast.`;
});

onMounted(() => {
  if (!allowed.value) return;
  void load();
  void loadToday();
});
</script>

<style scoped>
.forecast-pulse {
  display: grid;
  gap: 1px;
  flex-shrink: 0;
  margin: 0 0 12px;
  padding: 12px 14px 13px;
  border: 1px solid rgba(134, 217, 197, 0.22);
  border-radius: var(--radius-lg);
  background: rgba(134, 217, 197, 0.07);
  color: var(--color-shell-text);
  text-decoration: none;
  transition: background var(--transition-fast), border-color var(--transition-fast);
}

.forecast-pulse:hover {
  border-color: rgba(134, 217, 197, 0.45);
  background: rgba(134, 217, 197, 0.12);
}

.forecast-pulse:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

.pulse-head {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
  color: var(--color-model-on-shell);
  font-size: var(--text-xs);
  font-weight: 650;
}

.pulse-icon {
  font-size: 15px;
}

.pulse-month {
  color: var(--color-shell-muted);
  font-size: var(--text-xs);
}

.pulse-value {
  color: var(--color-on-shell);
  font-size: 20px;
  font-weight: 650;
  letter-spacing: -0.01em;
  line-height: 1.25;
  font-variant-numeric: lining-nums tabular-nums;
}

.pulse-range,
.pulse-peak {
  color: var(--color-shell-muted);
  font-size: var(--text-xs);
  font-variant-numeric: lining-nums tabular-nums;
}

.pulse-peak {
  margin-top: 4px;
  color: var(--color-model-on-shell);
}

/* At tablet and phone widths the sidebar is the account sheet; the forecast lives on Home. */
@media (max-width: 1024px) {
  .forecast-pulse {
    display: none;
  }
}
</style>
