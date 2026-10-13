<template>
  <div class="period-select" :class="{ 'is-custom': period === 'custom' }">
    <label class="period-picker">
      <span class="visually-hidden">Period</span>
      <UiIcon name="calendar" class="period-icon" />
      <select :value="period" @change="choose(($event.target as HTMLSelectElement).value)">
        <option v-for="option in periodOptions" :key="option.key" :value="option.key">{{ option.label }}</option>
      </select>
    </label>

    <div v-if="period === 'custom'" class="period-range">
      <label class="period-date">
        <span>From</span>
        <input
          type="date"
          :value="from"
          :max="to || undefined"
          @change="emit('update:from', ($event.target as HTMLInputElement).value)"
        />
      </label>
      <label class="period-date">
        <span>To</span>
        <input
          type="date"
          :value="to"
          :min="from || undefined"
          @change="emit('update:to', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import { isPeriodKey, periodOptions, resolvePeriod, type PeriodKey } from '../periods';
import UiIcon from './UiIcon.vue';

const props = defineProps<{ period: PeriodKey; from: string; to: string; today: string }>();
const emit = defineEmits<{
  'update:period': [PeriodKey];
  'update:from': [string];
  'update:to': [string];
}>();

function choose(value: string) {
  if (!isPeriodKey(value)) return;
  // A custom range starts from the current month rather than two empty boxes.
  if (value === 'custom' && !props.from && !props.to) {
    const month = resolvePeriod('month', props.today);
    if (month) {
      emit('update:from', month.from);
      emit('update:to', month.to);
    }
  }
  emit('update:period', value);
}
</script>

<style scoped>
.period-select {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.period-picker {
  position: relative;
  display: flex;
  min-width: 0;
}

.period-icon {
  position: absolute;
  top: 50%;
  left: 11px;
  color: var(--color-text-muted);
  font-size: 16px;
  transform: translateY(-50%);
  pointer-events: none;
}

.period-picker select {
  width: 100%;
  padding-left: 34px;
  cursor: pointer;
}

.period-range {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.period-date {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.period-date input {
  width: 156px;
}

@media (max-width: 860px) {
  .period-picker {
    flex: 1 1 100%;
  }
}

@media (max-width: 640px) {
  .period-range {
    width: 100%;
  }

  .period-date {
    flex: 1 1 0;
    min-width: 0;
  }

  .period-date input {
    width: 100%;
    min-width: 0;
  }
}
</style>
