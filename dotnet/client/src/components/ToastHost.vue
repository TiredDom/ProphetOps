<template>
  <div class="toast-host" role="status" aria-live="polite" aria-atomic="false">
    <TransitionGroup name="toast">
      <div v-for="t in toasts" :key="t.id" class="toast" :class="`toast-${t.tone}`">
        <span class="toast-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <polyline v-if="t.tone === 'success'" points="5 12.5 10 17.5 19 7" />
            <template v-else-if="t.tone === 'error'">
              <line x1="12" y1="7" x2="12" y2="13" />
              <circle cx="12" cy="17" r="0.6" fill="currentColor" />
            </template>
            <template v-else>
              <line x1="12" y1="11" x2="12" y2="17" />
              <circle cx="12" cy="7.5" r="0.6" fill="currentColor" />
            </template>
          </svg>
        </span>
        <p class="toast-message">{{ t.message }}</p>
        <button class="toast-close" type="button" aria-label="Dismiss" @click="dismiss(t.id)">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
            <line x1="6" y1="6" x2="18" y2="18" />
            <line x1="18" y1="6" x2="6" y2="18" />
          </svg>
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<script setup lang="ts">
import { useToastList } from '../composables/useToast';

const { toasts, dismiss } = useToastList();
</script>

<style scoped>
.toast-host {
  position: fixed;
  bottom: var(--space-6);
  right: var(--space-6);
  z-index: var(--z-toast);
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  width: min(360px, calc(100vw - var(--space-8)));
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  pointer-events: auto;
}

.toast-icon {
  flex: none;
  display: grid;
  place-items: center;
  width: 22px;
  height: 22px;
  border-radius: var(--radius-pill);
  color: #fff;
  margin-top: 1px;
}

.toast-icon svg {
  width: 14px;
  height: 14px;
}

.toast-success .toast-icon {
  background: var(--color-success);
}

.toast-error .toast-icon {
  background: var(--color-danger);
}

.toast-info .toast-icon {
  background: var(--color-primary);
}

.toast-message {
  flex: 1;
  margin: 0;
  color: var(--color-text-primary);
  font-size: 13.5px;
  line-height: 1.45;
}

.toast-close {
  flex: none;
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

.toast-close:hover {
  color: var(--color-text-primary);
}

.toast-close svg {
  width: 14px;
  height: 14px;
}

.toast-enter-active {
  transition: transform var(--transition-normal), opacity var(--transition-normal);
}

.toast-leave-active {
  transition: transform var(--transition-fast), opacity var(--transition-fast);
  position: absolute;
  right: 0;
  width: 100%;
}

.toast-enter-from {
  opacity: 0;
  transform: translateY(12px);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(16px);
}

@media (prefers-reduced-motion: reduce) {
  .toast-enter-active,
  .toast-leave-active {
    transition: opacity var(--transition-fast);
  }

  .toast-enter-from,
  .toast-leave-to {
    transform: none;
  }
}
</style>
