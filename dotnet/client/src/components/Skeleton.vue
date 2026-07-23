<template>
  <div class="skeleton-stack" aria-hidden="true">
    <div v-for="n in rows" :key="n" class="skeleton-bar" :style="{ width: widths[(n - 1) % widths.length] }"></div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ rows?: number }>(), { rows: 5 });

const widths = ['100%', '94%', '97%', '89%', '92%', '85%'];
</script>

<style scoped>
.skeleton-stack {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-2) 0;
}

.skeleton-bar {
  height: 16px;
  border-radius: var(--radius-sm);
  background: linear-gradient(
    90deg,
    var(--color-surface-sunken) 0%,
    var(--color-border-subtle) 50%,
    var(--color-surface-sunken) 100%
  );
  background-size: 200% 100%;
  animation: skeleton-shimmer 1.4s ease-in-out infinite;
}

@keyframes skeleton-shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .skeleton-bar {
    animation: none;
    background: var(--color-surface-sunken);
  }
}
</style>
