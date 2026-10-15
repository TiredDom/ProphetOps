<template>
  <Transition name="selection-bar">
    <div v-if="count > 0" class="selection-bar" role="region" :aria-label="`${count} selected`">
      <p class="selection-count" aria-live="polite">
        <strong>{{ count }}</strong> selected<template v-if="detail"> · {{ detail }}</template>
      </p>
      <div class="selection-actions">
        <slot />
      </div>
      <button type="button" class="selection-clear" aria-label="Clear selection" @click="emit('clear')">Clear</button>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * The actions for several records at once. It rides along the bottom of the list while rows
 * are selected, so the action stays in reach however far down the list the selection goes.
 */
defineProps<{ count: number; detail?: string }>();
const emit = defineEmits<{ clear: [] }>();
</script>

<style scoped>
.selection-bar {
  position: sticky;
  bottom: 16px;
  z-index: 5;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px 16px;
  padding: 10px 12px 10px 16px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
}

.selection-count {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-variant-numeric: lining-nums tabular-nums;
}

.selection-count strong {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 650;
}

.selection-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-left: auto;
}

.selection-clear {
  padding: 0 4px;
  border: 0;
  background: none;
  color: var(--color-text-muted);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.selection-clear:hover {
  color: var(--color-text-primary);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.selection-bar-enter-active,
.selection-bar-leave-active {
  transition: opacity var(--transition-normal), transform var(--transition-normal);
}

.selection-bar-enter-from,
.selection-bar-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

/* Phones: above the tab bar, the actions on a row of their own. */
@media (max-width: 1024px) {
  .selection-bar {
    bottom: calc(var(--tab-bar-height) + env(safe-area-inset-bottom, 0px) + 12px);
  }
}

@media (max-width: 640px) {
  .selection-actions {
    order: 3;
    width: 100%;
    margin-left: 0;
  }

  .selection-actions :deep(button) {
    flex: 1 1 0;
  }

  .selection-clear {
    margin-left: auto;
  }
}

@media (prefers-reduced-motion: reduce) {
  .selection-bar-enter-active,
  .selection-bar-leave-active {
    transition: opacity var(--transition-fast);
  }

  .selection-bar-enter-from,
  .selection-bar-leave-to {
    transform: none;
  }
}
</style>
