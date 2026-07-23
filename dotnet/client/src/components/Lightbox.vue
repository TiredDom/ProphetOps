<template>
  <Transition name="lightbox">
    <div
      v-if="url"
      ref="panel"
      class="lightbox"
      role="dialog"
      aria-modal="true"
      :aria-label="label"
      tabindex="-1"
      @click.self="emit('close')"
    >
      <figure class="lightbox-figure">
        <img :src="url" :alt="label" />
        <figcaption v-if="caption" class="lightbox-caption">{{ caption }}</figcaption>
      </figure>

      <button class="lightbox-close" type="button" aria-label="Close photo" @click="emit('close')">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true">
          <line x1="6" y1="6" x2="18" y2="18" />
          <line x1="18" y1="6" x2="6" y2="18" />
        </svg>
      </button>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useModalFocus } from '../composables/useModalFocus';

const props = defineProps<{ url: string | null; caption?: string }>();
const emit = defineEmits<{ close: [] }>();

const panel = ref<HTMLElement | null>(null);
const label = computed(() => (props.caption ? `Photo of ${props.caption}` : 'Photo'));

useModalFocus(panel, () => !!props.url);

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.url) emit('close');
}

onMounted(() => document.addEventListener('keydown', onKeydown));
onUnmounted(() => document.removeEventListener('keydown', onKeydown));
</script>

<style scoped>
.lightbox {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: grid;
  place-items: center;
  padding: var(--space-6);
  background: rgba(10, 22, 44, 0.82);
  outline: none;
  cursor: zoom-out;
}

.lightbox-figure {
  margin: 0;
  cursor: default;
}

.lightbox-figure img {
  display: block;
  max-width: min(1100px, 92vw);
  max-height: 84vh;
  object-fit: contain;
  border-radius: var(--radius-md);
  background: var(--color-surface);
  box-shadow: 0 24px 64px rgba(0, 0, 0, 0.45);
}

.lightbox-caption {
  margin-top: 10px;
  color: #EAF0FB;
  font-size: 13.5px;
  text-align: center;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}

.lightbox-close {
  position: absolute;
  top: 14px;
  right: 14px;
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border: none;
  border-radius: var(--radius-pill);
  background: rgba(255, 255, 255, 0.12);
  color: #FFFFFF;
  cursor: pointer;
  transition: background var(--transition-fast);
}

.lightbox-close:hover {
  background: rgba(255, 255, 255, 0.24);
}

.lightbox-close svg {
  width: 20px;
  height: 20px;
}

.lightbox-enter-active,
.lightbox-leave-active {
  transition: opacity 180ms var(--ease-out);
}

.lightbox-enter-from,
.lightbox-leave-to {
  opacity: 0;
}

@media (prefers-reduced-motion: reduce) {
  .lightbox-enter-active,
  .lightbox-leave-active {
    transition: none;
  }
}
</style>
