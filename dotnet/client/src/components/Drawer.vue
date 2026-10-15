<template>
  <Transition name="scrim">
    <div
      v-if="open"
      class="slide-drawer-backdrop"
      aria-hidden="true"
      @click="requestClose"
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
        <button class="slide-drawer-close" type="button" aria-label="Close panel" @click="requestClose">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </header>

      <div class="slide-drawer-body">
        <slot />
      </div>

      <!-- Closing a form with unsaved changes asks first, in place of the footer's buttons. -->
      <footer v-if="confirming" class="slide-drawer-footer drawer-discard" role="alertdialog" aria-labelledby="drawer-discard-text">
        <p id="drawer-discard-text" class="drawer-discard-text">
          {{ leaveTo ? 'Leave this page and lose your changes?' : 'Discard the changes you made?' }}
        </p>
        <button ref="keepButton" class="secondary-button" type="button" @click="keepEditing">Keep editing</button>
        <button class="danger-button" type="button" @click="discard">
          {{ leaveTo ? 'Leave page' : 'Discard changes' }}
        </button>
      </footer>
      <footer v-else-if="$slots.footer" class="slide-drawer-footer">
        <slot name="footer" :request-close="requestClose" />
      </footer>
    </aside>
  </Transition>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import { useModalFocus } from '../composables/useModalFocus';

/**
 * A side panel for a record or a form. With `dirty` set, every way out (the close button,
 * Esc, the backdrop, Cancel through the footer's `requestClose`, leaving the page, reloading)
 * asks before throwing the changes away.
 */
const props = withDefaults(defineProps<{ open: boolean; title: string; dirty?: boolean }>(), { dirty: false });
const emit = defineEmits<{ close: [] }>();

const router = useRouter();
const panel = ref<HTMLElement | null>(null);
const keepButton = ref<HTMLButtonElement | null>(null);
const confirming = ref(false);
const leaveTo = ref<string | null>(null);
let leaving = false;

useModalFocus(panel, () => props.open);

async function ask() {
  confirming.value = true;
  await nextTick();
  keepButton.value?.focus();
}

function requestClose() {
  if (props.dirty && props.open) {
    leaveTo.value = null;
    void ask();
    return;
  }
  emit('close');
}

function keepEditing() {
  confirming.value = false;
  leaveTo.value = null;
}

function discard() {
  const target = leaveTo.value;
  confirming.value = false;
  leaveTo.value = null;
  emit('close');
  if (target) {
    leaving = true;
    void router.push(target).finally(() => {
      leaving = false;
    });
  }
}

watch(
  () => props.open,
  (open) => {
    if (!open) {
      confirming.value = false;
      leaveTo.value = null;
    }
  },
);

onBeforeRouteLeave((to) => {
  if (leaving || !props.open || !props.dirty) return true;
  leaveTo.value = to.fullPath;
  void ask();
  return false;
});

function onKeydown(event: KeyboardEvent) {
  if (event.key !== 'Escape' || !props.open) return;
  // A lightbox or menu opened from inside the panel closes first.
  if (event.defaultPrevented) return;
  if (confirming.value) keepEditing();
  else requestClose();
}

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (!props.open || !props.dirty) return;
  event.preventDefault();
  event.returnValue = '';
}

onMounted(() => {
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('beforeunload', onBeforeUnload);
});
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown);
  window.removeEventListener('beforeunload', onBeforeUnload);
});

defineExpose({ requestClose });
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

.drawer-discard {
  flex-wrap: wrap;
  background: var(--tone-warning-surface);
  border-top-color: var(--tone-warning-border);
}

.drawer-discard-text {
  flex: 1 1 200px;
  color: var(--tone-warning-ink);
  font-size: var(--text-sm);
  font-weight: 600;
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

/* On phones the panel is the whole screen, so it needs no edge or shadow. */
@media (max-width: 640px) {
  .slide-drawer {
    width: 100vw;
    max-width: 100vw;
    border-left: none;
    box-shadow: none;
  }

  .slide-drawer-footer {
    padding: var(--space-3) var(--space-4) calc(var(--space-3) + env(safe-area-inset-bottom, 0px));
  }
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
