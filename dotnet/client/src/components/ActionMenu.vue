<template>
  <div ref="root" class="action-menu" @focusout="onFocusOut">
    <button
      :id="triggerId"
      ref="trigger"
      class="secondary-button action-menu-trigger"
      type="button"
      aria-haspopup="menu"
      :aria-expanded="open"
      :aria-controls="open ? menuId : undefined"
      @click="toggle"
      @keydown.down.prevent="show(0)"
      @keydown.up.prevent="show(items.length - 1)"
    >
      <UiIcon name="more" class="action-menu-icon" />
      <span>{{ label }}</span>
    </button>

    <Transition name="action-menu">
      <div
        v-if="open"
        :id="menuId"
        class="action-menu-list"
        role="menu"
        :aria-labelledby="triggerId"
        @keydown="onMenuKeydown"
      >
        <template v-for="item in items" :key="item.label">
          <a
            v-if="item.href"
            class="action-menu-item"
            role="menuitem"
            tabindex="-1"
            :href="item.href"
            @click="choose(item)"
          >
            <UiIcon v-if="item.icon" :name="item.icon" class="action-menu-item-icon" />
            <span class="action-menu-item-text">
              <span class="action-menu-item-label">{{ item.label }}</span> <span v-if="item.hint" class="action-menu-item-hint">{{ item.hint }}</span>
            </span>
          </a>
          <button
            v-else
            class="action-menu-item"
            role="menuitem"
            tabindex="-1"
            type="button"
            @click="choose(item)"
          >
            <UiIcon v-if="item.icon" :name="item.icon" class="action-menu-item-icon" />
            <span class="action-menu-item-text">
              <span class="action-menu-item-label">{{ item.label }}</span> <span v-if="item.hint" class="action-menu-item-hint">{{ item.hint }}</span>
            </span>
          </button>
        </template>
      </div>
    </Transition>
  </div>
</template>

<script lang="ts">
import type { UiIconName } from './UiIcon.vue';

export interface ActionMenuItem {
  label: string;
  hint?: string;
  icon?: UiIconName;
  /** A link (such as a file download); otherwise `run` is called. */
  href?: string;
  run?: () => void;
}

let nextId = 0;
</script>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue';
import UiIcon from './UiIcon.vue';

withDefaults(defineProps<{ items: ActionMenuItem[]; label?: string }>(), { label: 'More' });

const id = ++nextId;
const triggerId = `action-menu-trigger-${id}`;
const menuId = `action-menu-${id}`;

const open = ref(false);
const root = ref<HTMLElement | null>(null);
const trigger = ref<HTMLButtonElement | null>(null);

async function show(index = 0) {
  open.value = true;
  await nextTick();
  focusEntry(index);
}

function hide(returnFocus = false) {
  open.value = false;
  if (returnFocus) trigger.value?.focus();
}

function toggle() {
  if (open.value) hide();
  else void show(0);
}

function entries(): HTMLElement[] {
  return Array.from(root.value?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
}

function focusEntry(index: number) {
  const list = entries();
  if (list.length === 0) return;
  const wrapped = (index + list.length) % list.length;
  list[wrapped]?.focus();
}

function onMenuKeydown(event: KeyboardEvent) {
  const list = entries();
  const current = list.indexOf(document.activeElement as HTMLElement);
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault();
      focusEntry(current + 1);
      break;
    case 'ArrowUp':
      event.preventDefault();
      focusEntry(current - 1);
      break;
    case 'Home':
      event.preventDefault();
      focusEntry(0);
      break;
    case 'End':
      event.preventDefault();
      focusEntry(list.length - 1);
      break;
    case 'Escape':
      event.preventDefault();
      event.stopPropagation();
      hide(true);
      break;
    case 'Tab':
      hide();
      break;
  }
}

function choose(item: ActionMenuItem) {
  hide(!item.href);
  item.run?.();
}

function onFocusOut(event: FocusEvent) {
  const next = event.relatedTarget as Node | null;
  if (open.value && next && !root.value?.contains(next)) hide();
}

function onPointerDown(event: PointerEvent) {
  if (!root.value?.contains(event.target as Node)) hide();
}

watch(open, (isOpen) => {
  if (isOpen) document.addEventListener('pointerdown', onPointerDown);
  else document.removeEventListener('pointerdown', onPointerDown);
});

onBeforeUnmount(() => document.removeEventListener('pointerdown', onPointerDown));

defineExpose({ hide });
</script>

<style scoped>
.action-menu {
  position: relative;
  display: inline-flex;
}

.action-menu-icon {
  font-size: 18px;
}

.action-menu-list {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: var(--z-dropdown);
  display: grid;
  min-width: 248px;
  padding: 4px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
  transform-origin: top right;
}

.action-menu-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  width: 100%;
  padding: 9px 10px;
  border: 0;
  border-radius: var(--radius-md);
  background: none;
  color: var(--color-text-primary);
  font: inherit;
  font-size: var(--text-base);
  text-align: left;
  text-decoration: none;
  cursor: pointer;
}

.action-menu-item:hover,
.action-menu-item:focus-visible {
  outline: none;
  background: var(--color-surface-hover);
}

.action-menu-item:focus-visible {
  box-shadow: inset 0 0 0 2px var(--color-ring);
}

.action-menu-item-icon {
  margin-top: 2px;
  color: var(--color-text-muted);
  font-size: 16px;
}

.action-menu-item-text {
  display: grid;
  gap: 1px;
  min-width: 0;
}

.action-menu-item-label {
  font-weight: 500;
}

.action-menu-item-hint {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

/* On phones the menu button starts the actions row at the left edge, so the list opens from its
   left side instead of running off the screen. */
@media (max-width: 640px) {
  .action-menu-list {
    right: auto;
    left: 0;
    transform-origin: top left;
  }
}

.action-menu-enter-active,
.action-menu-leave-active {
  transition: opacity 140ms var(--ease-out), transform 140ms var(--ease-out);
}

.action-menu-enter-from,
.action-menu-leave-to {
  opacity: 0;
  transform: translateY(-4px) scale(0.98);
}
</style>
