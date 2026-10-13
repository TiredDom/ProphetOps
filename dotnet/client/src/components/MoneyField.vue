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
  width: 100%;
  min-width: 0;
  min-height: 38px;
  padding: 0 10px;
  background: var(--color-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.money-field:hover {
  border-color: var(--color-text-faint);
}

.money-field.focused {
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.money-field-prefix {
  color: var(--color-text-muted);
  font-size: var(--text-base);
  font-weight: 600;
}

/* The wrapper draws the field; the inner input stays bare so it does not pick up a second
   border or focus ring from the shared input styles. */
.money-field input {
  flex: 1;
  min-width: 0;
  min-height: 0;
  border: none;
  border-radius: 0;
  background: none;
  box-shadow: none;
  padding: 8px 0;
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-variant-numeric: lining-nums tabular-nums;
}

.money-field input:focus {
  outline: none;
  box-shadow: none;
}

@media (max-width: 860px) {
  .money-field {
    min-height: 44px;
  }

  .money-field input {
    font-size: 16px;
  }
}
</style>
