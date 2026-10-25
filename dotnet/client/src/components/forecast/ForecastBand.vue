<template>
  <section
    class="forecast-band"
    :class="{ folded: folded || pending }"
    :aria-labelledby="titleId"
    :aria-busy="pending ? 'true' : undefined"
  >
    <div class="band-head">
      <span class="band-mark" aria-hidden="true"><UiIcon name="forecast" :stroke-width="2" /></span>
      <div class="band-titles">
        <h2 :id="titleId" class="band-title">{{ title }}</h2>
        <p v-if="subtitle" class="band-subtitle">{{ subtitle }}</p>
      </div>
      <div v-if="$slots.action" class="band-action">
        <slot name="action" />
      </div>
      <button
        v-if="summary && !pending && !compact && !folded"
        type="button"
        class="band-toggle"
        aria-expanded="true"
        :aria-controls="bodyId"
        @click="toggle"
      >
        Hide details
        <UiIcon name="chevron-down" class="band-toggle-icon" />
      </button>
    </div>

    <!-- While the forecast loads, a line the size of the folded summary holds its place, so the
         list below does not jump when it arrives. -->
    <div v-if="pending" class="band-fold" aria-hidden="true">
      <span class="band-pending"></span>
    </div>

    <!-- Folded to one line by default, so the list below comes first. On wide screens opening one
         band opens them on every page, and the choice is remembered. -->
    <div v-else-if="summary && (compact || folded)" class="band-fold">
      <p class="band-summary">{{ summary }}</p>
      <button
        type="button"
        class="band-toggle"
        :aria-expanded="!folded"
        :aria-controls="bodyId"
        @click="toggle"
      >
        {{ folded ? 'Show details' : 'Hide details' }}
        <UiIcon name="chevron-down" class="band-toggle-icon" />
      </button>
    </div>

    <div v-if="!pending" v-show="!folded" :id="bodyId" class="band-body">
      <slot />
      <button type="button" class="band-help" @click="openHelp(helpTopic)">
        <UiIcon name="help" /> {{ helpTopic === 'forecast-decisions' ? 'How to use these suggestions' : 'How the forecast works' }}
      </button>
    </div>
  </section>
</template>

<script lang="ts">
import { ref as sharedRef } from 'vue';

let nextId = 0;
const FOLD_KEY = 'prophetops.forecastBands';

function readFolded(): boolean {
  try {
    return localStorage.getItem(FOLD_KEY) !== 'open';
  } catch {
    return true;
  }
}

// One choice for every band on wide screens: folded until someone opens one, then open on every page.
const wideFolded = sharedRef(readFolded());
</script>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useMediaQuery } from '../../composables/useMediaQuery';
import UiIcon from '../UiIcon.vue';
import { openHelp } from '../../composables/useHelp';

/** `pending` is set while the forecast is still on its way: the band keeps its place without figures. */
const props = withDefaults(
  defineProps<{ title: string; subtitle?: string; summary?: string; helpTopic?: string; pending?: boolean }>(),
  { subtitle: '', summary: '', helpTopic: 'read-forecast', pending: false },
);

const id = ++nextId;
const titleId = `forecast-band-${id}`;
const bodyId = `forecast-band-body-${id}`;

const compact = useMediaQuery('(max-width: 640px)');
const expanded = ref(false);
const folded = computed(() => !!props.summary && (compact.value ? !expanded.value : wideFolded.value));

function toggle() {
  if (compact.value) {
    expanded.value = !expanded.value;
    return;
  }
  wideFolded.value = !wideFolded.value;
  try {
    localStorage.setItem(FOLD_KEY, wideFolded.value ? 'folded' : 'open');
  } catch {
    // The choice still holds for this visit.
  }
}
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
  color: var(--color-on-shell);
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
  background: var(--tint-model-on-shell);
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
  color: var(--color-on-shell);
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
  color: var(--color-on-shell-soft);
  font-size: var(--text-sm);
  font-variant-numeric: lining-nums tabular-nums;
}

/* As tall as the folded summary's toggle, so the band keeps its height when the figures arrive. */
.band-pending {
  display: block;
  width: min(320px, 70%);
  height: 14px;
  margin: 11px 0;
  border-radius: var(--radius-sm);
  background: rgba(255, 255, 255, 0.12);
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
  color: var(--color-on-shell);
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
  color: var(--color-on-shell);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.band-body :deep(.band-model) {
  color: var(--color-model-on-shell);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.band-body :deep(.band-muted) {
  color: var(--color-on-shell-muted);
}

.band-body :deep(.band-warning) {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  color: var(--color-on-shell-warning);
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

.band-help {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  justify-self: start;
  min-height: 28px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-on-shell-muted);
  font: inherit;
  font-size: var(--text-xs);
  font-weight: 600;
  cursor: pointer;
}

.band-help:hover {
  color: var(--color-on-shell);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.band-help:focus-visible {
  outline: 2px solid var(--color-model-on-shell);
  outline-offset: 2px;
}

.band-help .ui-icon {
  font-size: 14px;
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
