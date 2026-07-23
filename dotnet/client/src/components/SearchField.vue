<template>
  <div class="search-field">
    <span class="search-field-icon" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="10.5" cy="10.5" r="6.5" />
        <line x1="15.5" y1="15.5" x2="20" y2="20" />
      </svg>
    </span>
    <input
      :value="modelValue"
      :placeholder="placeholder ?? 'Search'"
      type="text"
      :aria-label="placeholder ?? 'Search'"
      @input="onInput"
    />
    <button v-if="modelValue" class="search-field-clear" type="button" aria-label="Clear search" @click="clear">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
        <line x1="6" y1="6" x2="18" y2="18" />
        <line x1="18" y1="6" x2="6" y2="18" />
      </svg>
    </button>
  </div>
</template>

<script setup lang="ts">
defineProps<{ modelValue: string; placeholder?: string }>();
const emit = defineEmits<{ 'update:modelValue': [string] }>();

function onInput(event: Event) {
  emit('update:modelValue', (event.target as HTMLInputElement).value);
}

function clear() {
  emit('update:modelValue', '');
}
</script>

<style scoped>
.search-field {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  width: min(320px, 100%);
  padding: 0 var(--space-3);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.search-field:focus-within {
  border-color: var(--color-ring);
  box-shadow: var(--focus-ring);
}

.search-field-icon {
  display: grid;
  place-items: center;
  color: var(--color-text-faint);
}

.search-field-icon svg {
  width: 16px;
  height: 16px;
}

.search-field input {
  flex: 1;
  min-width: 0;
  border: none;
  background: none;
  padding: 9px 0;
  color: var(--color-text-primary);
  font-size: 13.5px;
}

@media (max-width: 860px) {
  .search-field input {
    font-size: 16px;
  }
}

.search-field input:focus {
  outline: none;
}

.search-field input::placeholder {
  color: var(--color-text-muted);
}

.search-field-clear {
  display: grid;
  place-items: center;
  width: 26px;
  height: 26px;
  padding: 0;
  border: none;
  background: none;
  color: var(--color-text-muted);
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: color var(--transition-fast);
}

.search-field-clear:hover {
  color: var(--color-text-primary);
}

.search-field-clear svg {
  width: 14px;
  height: 14px;
}
</style>
