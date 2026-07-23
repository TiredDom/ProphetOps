<template>
  <Transition name="scrim">
    <div
      v-if="open"
      class="slide-drawer-backdrop"
      aria-hidden="true"
      @click="emit('close')"
    ></div>
  </Transition>

  <Transition name="panel">
    <aside
      v-if="open"
      ref="panel"
      class="slide-drawer"
      role="dialog"
      aria-modal="true"
      :aria-label="title"
      tabindex="-1"
    >
      <header class="slide-drawer-header">
        <h2 class="slide-drawer-title">{{ title }}</h2>
        <button class="slide-drawer-close" type="button" aria-label="Close panel" @click="emit('close')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </header>

      <div class="slide-drawer-body">
        <slot />
      </div>

      <footer v-if="$slots.footer" class="slide-drawer-footer">
        <slot name="footer" />
      </footer>
    </aside>
  </Transition>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { useModalFocus } from '../composables/useModalFocus';

const props = defineProps<{ open: boolean; title: string }>();
const emit = defineEmits<{ close: [] }>();

const panel = ref<HTMLElement | null>(null);

useModalFocus(panel, () => props.open);

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.open) {
    emit('close');
  }
}

onMounted(() => document.addEventListener('keydown', onKeydown));
onUnmounted(() => document.removeEventListener('keydown', onKeydown));
</script>

<style scoped>
.slide-drawer-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(20, 33, 61, 0.4);
  border: none;
  z-index: var(--z-drawer-backdrop);
  cursor: pointer;
}

.slide-drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 460px;
  max-width: 92vw;
  background: var(--color-surface);
  border-left: 1px solid var(--color-border);
  box-shadow: -16px 0 40px rgba(14, 34, 71, 0.14);
  z-index: var(--z-drawer);
  display: flex;
  flex-direction: column;
  outline: none;
}

.slide-drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-5) var(--space-6);
  border-bottom: 1px solid var(--color-border);
}

.slide-drawer-title {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 20px;
  font-weight: 560;
  letter-spacing: -0.005em;
  color: var(--color-text-primary);
}

.slide-drawer-close {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  flex: none;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.slide-drawer-close:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text-primary);
}

.slide-drawer-close svg {
  width: 20px;
  height: 20px;
}

.slide-drawer-body {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-6);
}

.slide-drawer-footer {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-6);
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
}

.scrim-enter-active,
.scrim-leave-active {
  transition: opacity 200ms var(--ease-out);
}

.scrim-enter-from,
.scrim-leave-to {
  opacity: 0;
}

.panel-enter-active,
.panel-leave-active {
  transition: transform 200ms var(--ease-out);
}

.panel-enter-from,
.panel-leave-to {
  transform: translateX(100%);
}

@media (prefers-reduced-motion: reduce) {
  .scrim-enter-active,
  .scrim-leave-active,
  .panel-enter-active,
  .panel-leave-active {
    transition: none;
  }
}
</style>
