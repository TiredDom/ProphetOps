<template>
  <svg
    class="ui-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    :stroke-width="strokeWidth"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <template v-if="name === 'plus'">
      <line x1="12" y1="5.5" x2="12" y2="18.5" />
      <line x1="5.5" y1="12" x2="18.5" y2="12" />
    </template>
    <template v-else-if="name === 'pencil'">
      <path d="M5 19h3.5L18.2 9.3a2.2 2.2 0 0 0-3.1-3.1L5.4 15.9Z" />
      <line x1="13.6" y1="7.7" x2="16.7" y2="10.8" />
    </template>
    <template v-else-if="name === 'void'">
      <circle cx="12" cy="12" r="7.5" />
      <line x1="6.8" y1="6.8" x2="17.2" y2="17.2" />
    </template>
    <template v-else-if="name === 'restore'">
      <path d="M5.2 13a7 7 0 1 0 1.9-5.6" />
      <polyline points="5 4.5 5 9 9.5 9" />
    </template>
    <template v-else-if="name === 'upload'">
      <line x1="12" y1="15" x2="12" y2="4.5" />
      <polyline points="7.5 9 12 4.5 16.5 9" />
      <path d="M5 15.5V19h14v-3.5" />
    </template>
    <template v-else-if="name === 'wallet'">
      <path d="M4.5 7.5h13a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-13a1 1 0 0 1-1-1v-10a2.5 2.5 0 0 1 2.5-2.5h10.5" />
      <path d="M15.5 13.5h4v-3h-4a1.5 1.5 0 0 0 0 3Z" />
    </template>
    <template v-else-if="name === 'ticket'">
      <path d="M4 7.5A1.5 1.5 0 0 1 5.5 6h13A1.5 1.5 0 0 1 20 7.5v2a2.5 2.5 0 0 0 0 5v2a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 16.5v-2a2.5 2.5 0 0 0 0-5Z" />
      <line x1="14.5" y1="6.5" x2="14.5" y2="8" />
      <line x1="14.5" y1="11.25" x2="14.5" y2="12.75" />
      <line x1="14.5" y1="16" x2="14.5" y2="17.5" />
    </template>
    <template v-else-if="name === 'check'">
      <polyline points="5.5 12.5 10 17 18.5 7.5" />
    </template>
    <template v-else-if="name === 'calendar'">
      <rect x="4" y="5.5" width="16" height="14.5" rx="2" />
      <line x1="4" y1="10" x2="20" y2="10" />
      <line x1="8.5" y1="3.5" x2="8.5" y2="7" />
      <line x1="15.5" y1="3.5" x2="15.5" y2="7" />
    </template>
    <template v-else-if="name === 'table'">
      <rect x="3.5" y="5" width="17" height="14" rx="2" />
      <line x1="3.5" y1="9.5" x2="20.5" y2="9.5" />
      <line x1="3.5" y1="14.25" x2="20.5" y2="14.25" />
      <line x1="9.5" y1="9.5" x2="9.5" y2="19" />
    </template>
    <template v-else-if="name === 'chevron-right'">
      <polyline points="9.5 6 15.5 12 9.5 18" />
    </template>
    <template v-else-if="name === 'chevron-down'">
      <polyline points="6 9.5 12 15.5 18 9.5" />
    </template>
    <template v-else-if="name === 'arrow-right'">
      <line x1="5" y1="12" x2="18.5" y2="12" />
      <polyline points="13 6.5 18.5 12 13 17.5" />
    </template>
    <template v-else-if="name === 'arrow-down'">
      <line x1="12" y1="5" x2="12" y2="18.5" />
      <polyline points="6.5 13 12 18.5 17.5 13" />
    </template>
    <template v-else-if="name === 'arrow-up'">
      <line x1="12" y1="19" x2="12" y2="5.5" />
      <polyline points="6.5 11 12 5.5 17.5 11" />
    </template>
    <template v-else-if="name === 'sort'">
      <polyline points="8 9.5 12 5.5 16 9.5" />
      <polyline points="8 14.5 12 18.5 16 14.5" />
    </template>
    <template v-else-if="name === 'more'">
      <circle cx="6" cy="12" r="1.7" fill="currentColor" stroke="none" />
      <circle cx="12" cy="12" r="1.7" fill="currentColor" stroke="none" />
      <circle cx="18" cy="12" r="1.7" fill="currentColor" stroke="none" />
    </template>
    <template v-else-if="name === 'download'">
      <line x1="12" y1="4.5" x2="12" y2="15" />
      <polyline points="7.5 10.5 12 15 16.5 10.5" />
      <path d="M5 15.5V19h14v-3.5" />
    </template>
    <template v-else-if="name === 'image'">
      <rect x="3.5" y="5" width="17" height="14" rx="2" />
      <circle cx="9" cy="10" r="1.6" />
      <path d="M4 17.5 8.5 13l3.5 3 3-2.5 5 4.5" />
    </template>
    <!-- Recorded line turning into a dashed forecast: the mark for anything the model produced. -->
    <template v-else-if="name === 'forecast'">
      <polyline points="3.5 17.5 8 13 11 15 13.5 11.5" />
      <polyline points="13.5 11.5 16.8 8.2 20.5 6" stroke-dasharray="1.4 2.6" />
      <circle cx="13.5" cy="11.5" r="1.5" fill="currentColor" stroke="none" />
    </template>
    <template v-else-if="name === 'alert'">
      <path d="M12 4.5 20.5 19h-17Z" />
      <line x1="12" y1="10" x2="12" y2="13.8" />
      <circle cx="12" cy="16.4" r="0.9" fill="currentColor" stroke="none" />
    </template>
    <template v-else-if="name === 'printer'">
      <path d="M7 9V4.5h10V9" />
      <rect x="3.5" y="9" width="17" height="7.5" rx="1.5" />
      <path d="M7 13.5h10V20H7Z" />
    </template>
    <template v-else-if="name === 'close'">
      <line x1="6.5" y1="6.5" x2="17.5" y2="17.5" />
      <line x1="17.5" y1="6.5" x2="6.5" y2="17.5" />
    </template>
    <!-- Direction markers are solid so they read at small sizes next to figures. -->
    <template v-else-if="name === 'up'">
      <polygon points="12 6 19 17 5 17" fill="currentColor" stroke="none" />
    </template>
    <template v-else-if="name === 'down'">
      <polygon points="5 7 19 7 12 18" fill="currentColor" stroke="none" />
    </template>
  </svg>
</template>

<script lang="ts">
export type UiIconName =
  | 'plus'
  | 'pencil'
  | 'void'
  | 'restore'
  | 'upload'
  | 'wallet'
  | 'ticket'
  | 'check'
  | 'calendar'
  | 'table'
  | 'chevron-right'
  | 'chevron-down'
  | 'arrow-right'
  | 'arrow-down'
  | 'arrow-up'
  | 'sort'
  | 'more'
  | 'download'
  | 'image'
  | 'close'
  | 'printer'
  | 'forecast'
  | 'alert'
  | 'up'
  | 'down';
</script>

<script setup lang="ts">
withDefaults(defineProps<{ name: UiIconName; strokeWidth?: number }>(), { strokeWidth: 1.8 });
</script>

<style scoped>
.ui-icon {
  display: block;
  flex: none;
  width: 1em;
  height: 1em;
}
</style>
