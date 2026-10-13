<template>
  <section class="forecast-band" :class="{ folded }" :aria-labelledby="titleId">
    <div class="band-head">
      <span class="band-mark" aria-hidden="true"><UiIcon name="forecast" :stroke-width="2" /></span>
      <div class="band-titles">
        <h2 :id="titleId" class="band-title">{{ title }}</h2>
        <p v-if="subtitle" class="band-subtitle">{{ subtitle }}</p>
      </div>
      <div v-if="$slots.action" class="band-action">
        <slot name="action" />
      </div>
    </div>

    <!-- On phones the band opens folded to one line, so the list below stays within reach. -->
    <div v-if="compact && summary" class="band-fold">
      <p class="band-summary">{{ summary }}</p>
      <button
        type="button"
        class="band-toggle"
        :aria-expanded="expanded"
        :aria-controls="bodyId"
        @click="expanded = !expanded"
      >
        {{ expanded ? 'Hide details' : 'Show details' }}
        <UiIcon name="chevron-down" class="band-toggle-icon" />
      </button>
    </div>

    <div v-show="!folded" :id="bodyId" class="band-body">
      <slot />
    </div>
  </section>
</template>

<script lang="ts">
let nextId = 0;
</script>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useMediaQuery } from '../../composables/useMediaQuery';
import UiIcon from '../UiIcon.vue';

const props = defineProps<{ title: string; subtitle?: string; summary?: string }>();

const id = ++nextId;
const titleId = `forecast-band-${id}`;
const bodyId = `forecast-band-body-${id}`;

const compact = useMediaQuery('(max-width: 640px)');
const expanded = ref(false);
const folded = computed(() => compact.value && !!props.summary && !expanded.value);
</script>

<style scoped>
/* The model's voice outside Home: the same navy band and teal figures as the forecast summary,
   so anything Holt-Winters produced is recognisable on every page. */
.forecast-band {
  display: grid;
  gap: 14px;
  padding: 18px 24px 20px;
  border-radius: var(--radius-lg);
  background: var(--color-shell);
  color: #FFFFFF;
}

.band-head {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.band-mark {
  display: grid;
  place-items: center;
  flex: none;
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  background: rgba(134, 217, 197, 0.14);
  color: var(--color-model-on-shell);
  font-size: 20px;
}

.band-titles {
  display: grid;
  gap: 1px;
  flex: 1 1 auto;
  min-width: 0;
}

.band-title {
  color: #FFFFFF;
  font-size: var(--text-md);
  font-weight: 600;
  letter-spacing: -0.01em;
}

.band-subtitle {
  color: var(--color-shell-muted);
  font-size: var(--text-sm);
}

.band-action {
  flex: none;
}

.band-fold {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px 12px;
}

.band-summary {
  flex: 1 1 180px;
  min-width: 0;
  color: #DCE5F3;
  font-size: var(--text-sm);
  font-variant-numeric: lining-nums tabular-nums;
}

.band-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid rgba(255, 255, 255, 0.22);
  border-radius: var(--radius-pill);
  background: transparent;
  color: #FFFFFF;
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.band-toggle:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

.band-toggle-icon {
  font-size: 15px;
  transition: transform var(--transition-normal);
}

.band-toggle[aria-expanded='true'] .band-toggle-icon {
  transform: rotate(180deg);
}

.band-body {
  display: grid;
  gap: 12px;
  min-width: 0;
}

/* Shared pieces for what goes inside a band */
.band-body :deep(.band-figure) {
  color: #FFFFFF;
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.band-body :deep(.band-model) {
  color: var(--color-model-on-shell);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.band-body :deep(.band-muted) {
  color: #B9C7DE;
}

.band-body :deep(.band-warning) {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  color: #F3CF86;
  font-size: var(--text-sm);
  line-height: 1.5;
}

.band-body :deep(.band-warning .ui-icon) {
  margin-top: 2px;
  font-size: 16px;
}

.band-action :deep(.band-link) {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  color: var(--color-model-on-shell);
  font-size: var(--text-sm);
  font-weight: 600;
  text-decoration: none;
  white-space: nowrap;
}

.band-action :deep(.band-link:hover) {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.band-action :deep(.band-link:focus-visible) {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

@media (max-width: 640px) {
  .forecast-band {
    gap: 12px;
    padding: 16px;
  }

  .forecast-band.folded {
    gap: 10px;
  }

  /* Home is one tap away in the tab bar, so phones drop the link and the subtitle. */
  .band-subtitle,
  .band-action {
    display: none;
  }
}
</style>
