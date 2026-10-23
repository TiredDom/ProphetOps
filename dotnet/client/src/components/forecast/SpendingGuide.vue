<template>
  <ForecastBand
    help-topic="forecast-decisions"
    v-if="visible || pending"
    title="What the forecast suggests for spending"
    subtitle="From the revenue forecast. The decision is yours."
    :summary="`${items.length} ${items.length === 1 ? 'decision' : 'decisions'} on spending, from the forecast`"
    :pending="pending"
  >
    <template #action>
      <RouterLink to="/home" class="band-link">See the forecast <UiIcon name="arrow-right" /></RouterLink>
    </template>

    <ul class="guide">
      <li v-for="item in items" :key="item.rec.id" class="guide-item" :class="`kind-${item.rec.kind}`">
        <span class="guide-mark" aria-hidden="true">
          <UiIcon v-if="item.rec.kind === 'increase'" name="up" />
          <UiIcon v-else-if="item.rec.kind === 'decrease'" name="down" />
          <UiIcon v-else name="alert" />
        </span>
        <div class="guide-text">
          <p class="guide-action">{{ item.rec.action }}</p>
          <p class="guide-finding">{{ item.rec.finding }}</p>
        </div>
        <button
          v-if="item.lastYear"
          type="button"
          class="guide-button"
          :aria-pressed="activeFrom === item.lastYear.from"
          @click="emit('show-month', item.lastYear.from, item.lastYear.to)"
        >
          {{ activeFrom === item.lastYear.from ? 'Showing' : 'Show' }} {{ item.lastYear.label }}
        </button>
      </li>
    </ul>
  </ForecastBand>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useForecast } from '../../composables/useForecast';
import { buildForecastDecisions } from '../../forecastChart';
import { monthKeyOfLabel, sameMonthLastYear } from '../../forecastFacts';
import UiIcon from '../UiIcon.vue';
import ForecastBand from './ForecastBand.vue';

// `activeFrom` is the start of the period the ledger is showing, so the matching button can say so.
defineProps<{ activeFrom: string }>();
const emit = defineEmits<{ 'show-month': [from: string, to: string] }>();

const { state, allowed, load } = useForecast();

// The same decisions Home shows, each paired with the same month a year earlier: the season the
// forecast leans on, and the spending already on record for it.
const items = computed(() => {
  const data = state.data;
  if (!data?.ok || !data.steps?.length) return [];
  return buildForecastDecisions({ steps: data.steps, history: data.history }).map((rec) => {
    const stepNumber = rec.stepNumbers[rec.stepNumbers.length - 1];
    const step = data.steps.find((s) => s.step === stepNumber);
    const key = step ? monthKeyOfLabel(step.monthLabel) : null;
    return { rec, lastYear: key ? sameMonthLastYear(key) : null };
  });
});

const visible = computed(() => allowed.value && items.value.length > 0);
const pending = computed(() => allowed.value && !state.data && !state.error);

onMounted(() => {
  void load();
});
</script>

<style scoped>
.guide {
  display: grid;
  margin: 0;
  padding: 0;
  list-style: none;
}

.guide-item {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr) auto;
  align-items: center;
  gap: 4px 14px;
  padding: 10px 0;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.guide-item:first-child {
  padding-top: 2px;
  border-top: 0;
}

.guide-mark {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 12px;
}

.kind-increase .guide-mark {
  background: rgba(134, 217, 197, 0.16);
  color: var(--color-model-on-shell);
}

.kind-decrease .guide-mark {
  background: rgba(244, 163, 160, 0.16);
  color: var(--color-on-shell-danger);
}

.kind-uncertainty .guide-mark {
  background: rgba(243, 207, 134, 0.16);
  color: var(--color-on-shell-warning);
  font-size: 15px;
}

.guide-text {
  display: grid;
  gap: 1px;
  min-width: 0;
}

.guide-action {
  color: var(--color-on-shell);
  font-size: var(--text-base);
  font-weight: 600;
}

.guide-finding {
  color: var(--color-on-shell-muted);
  font-size: var(--text-sm);
}

.kind-increase .guide-finding {
  color: var(--color-model-on-shell);
}

.kind-decrease .guide-finding {
  color: var(--color-on-shell-danger);
}

.kind-uncertainty .guide-finding {
  color: var(--color-on-shell-warning);
}

.guide-button {
  padding: 5px 12px;
  border: 1px solid rgba(255, 255, 255, 0.25);
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-on-shell);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  white-space: nowrap;
  cursor: pointer;
}

.guide-button:hover {
  background: rgba(255, 255, 255, 0.08);
}

.guide-button[aria-pressed='true'] {
  border-color: var(--color-model-on-shell);
  background: rgba(134, 217, 197, 0.16);
  color: var(--color-model-on-shell);
}

.guide-button:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

@media (max-width: 640px) {
  .guide-item {
    grid-template-columns: 28px minmax(0, 1fr);
  }

  .guide-button {
    grid-column: 2;
    justify-self: start;
  }
}
</style>
