<template>
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <template v-if="name === 'home'">
      <path d="M4 10.5 12 4l8 6.5V19a1.5 1.5 0 0 1-1.5 1.5H15v-5.5H9v5.5H5.5A1.5 1.5 0 0 1 4 19Z" />
    </template>
    <template v-else-if="name === 'bookings'">
      <rect x="4" y="5" width="16" height="15" rx="2" />
      <line x1="4" y1="9.5" x2="20" y2="9.5" />
      <line x1="8.5" y1="3" x2="8.5" y2="6.5" />
      <line x1="15.5" y1="3" x2="15.5" y2="6.5" />
    </template>
    <template v-else-if="name === 'packages'">
      <path d="M12 3 3.5 7.5v9L12 21l8.5-4.5v-9L12 3Z" />
      <polyline points="3.5 7.5 12 12 20.5 7.5" />
      <line x1="12" y1="12" x2="12" y2="21" />
    </template>
    <template v-else-if="name === 'expenses'">
      <path d="M6.5 3.5h11v17l-2.2-1.5-2.2 1.5-2.2-1.5-2.2 1.5-2.2-1.5Z" />
      <line x1="9.5" y1="8.5" x2="14.5" y2="8.5" />
      <line x1="9.5" y1="12" x2="14.5" y2="12" />
    </template>
    <template v-else-if="name === 'reports'">
      <line x1="4" y1="20" x2="20" y2="20" />
      <line x1="7.5" y1="20" x2="7.5" y2="12" />
      <line x1="12" y1="20" x2="12" y2="7" />
      <line x1="16.5" y1="20" x2="16.5" y2="14.5" />
    </template>
    <template v-else-if="name === 'team'">
      <circle cx="9" cy="8.5" r="3.3" />
      <path d="M3.5 20c0-3.2 2.5-5.3 5.5-5.3s5.5 2.1 5.5 5.3" />
      <path d="M15.5 5.7a3.3 3.3 0 0 1 0 5.6" />
      <path d="M16.8 15c2.1.5 3.7 2.3 3.7 5" />
    </template>
  </svg>
</template>

<script setup lang="ts">
import type { NavIconName } from '../nav';

defineProps<{ name: NavIconName }>();
</script>
