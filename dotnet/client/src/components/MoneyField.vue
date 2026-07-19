<template>
  <div class="money-field" :class="{ focused }">
    <span class="money-field-prefix" aria-hidden="true">₱</span>
    <input
      :value="display"
      type="text"
      inputmode="numeric"
      autocomplete="off"
      @input="onInput"
      @focus="onFocus"
      @blur="onBlur"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { count, digitsOnly } from '../format';

const props = defineProps<{ modelValue: number }>();
const emit = defineEmits<{ 'update:modelValue': [number] }>();

const focused = ref(false);
const display = ref(count(props.modelValue));

watch(
  () => props.modelValue,
  (value) => {
    if (digitsOnly(display.value) !== value) display.value = focused.value ? String(value) : count(value);
  },
);

function onInput(event: Event) {
  const el = event.target as HTMLInputElement;
  const value = digitsOnly(el.value);
  display.value = el.value.replace(/[^\d]/g, '');
  el.value = display.value;
  emit('update:modelValue', value);
}

function onFocus() {
  focused.value = true;
  display.value = String(digitsOnly(display.value) || '');
}

function onBlur() {
  focused.value = false;
  display.value = count(digitsOnly(display.value));
}
</script>

<style scoped>
.money-field {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 var(--space-3);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.money-field.focused {
  border-color: var(--color-ring);
  box-shadow: var(--focus-ring);
}

.money-field-prefix {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 600;
}

.money-field input {
  flex: 1;
  min-width: 0;
  border: none;
  background: none;
  padding: 9px 0;
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-variant-numeric: lining-nums tabular-nums;
}

.money-field input:focus {
  outline: none;
}
</style>
