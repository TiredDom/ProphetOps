<template>
  <div v-if="total > shown || total > pageSize" class="list-footer">
    <p class="list-footer-count">
      Showing {{ count(shown) }} of {{ count(total) }} {{ noun }}
    </p>

    <div v-if="total > shown" class="list-footer-actions">
      <button class="table-link" type="button" @click="$emit('more')">
        Show {{ count(Math.min(pageSize, total - shown)) }} more
      </button>
      <button class="table-link list-footer-all" type="button" @click="$emit('all')">
        Show all
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { count } from '../format';

withDefaults(
  defineProps<{ shown: number; total: number; noun: string; pageSize?: number }>(),
  { pageSize: 25 },
);

defineEmits<{ more: []; all: [] }>();
</script>

<style scoped>
.list-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 10px 20px;
  padding-top: 12px;
  margin-top: 12px;
  border-top: 1px solid var(--color-border-subtle);
}

.list-footer-count {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12.5px;
  font-variant-numeric: lining-nums tabular-nums;
}

.list-footer-actions {
  display: flex;
  align-items: center;
  gap: 18px;
}

.list-footer-all {
  color: var(--color-text-muted);
}
</style>
