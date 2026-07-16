<template>
  <div class="empty-block">
    <span class="empty-icon" aria-hidden="true">
      <slot name="icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M4 7.5 12 3l8 4.5v9L12 21l-8-4.5Z" />
          <polyline points="4 7.5 12 12 20 7.5" />
          <line x1="12" y1="12" x2="12" y2="21" />
        </svg>
      </slot>
    </span>
    <h4 class="empty-title">{{ title }}</h4>
    <p v-if="message" class="empty-text">{{ message }}</p>
    <div v-if="$slots.action" class="empty-action">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ title: string; message?: string }>();
</script>

<style scoped>
.empty-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--space-2);
  padding: var(--space-10) var(--space-6);
  background: var(--color-surface);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  margin-bottom: var(--space-2);
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
  color: var(--color-text-faint);
}

.empty-icon svg {
  width: 22px;
  height: 22px;
}

.empty-title {
  margin: 0;
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 17px;
  font-weight: 560;
  color: var(--color-text-primary);
}

.empty-text {
  margin: 0;
  max-width: 42ch;
  color: var(--color-text-muted);
  font-size: 13.5px;
  line-height: 1.5;
}

.empty-action {
  margin-top: var(--space-4);
}
</style>
