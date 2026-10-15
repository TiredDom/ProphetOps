<template>
  <th scope="col" :class="{ num: align === 'right' }" :aria-sort="ariaSort">
    <button type="button" class="sort-button" :class="{ active }" @click="emit('sort', field)">
      <span>{{ label }}</span>
      <UiIcon :name="icon" class="sort-icon" />
    </button>
  </th>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import UiIcon from './UiIcon.vue';

// The page owns the sort order, written "field-asc" or "field-desc".
const props = withDefaults(defineProps<{ label: string; field: string; sort: string; align?: 'left' | 'right' }>(), {
  align: 'left',
});
const emit = defineEmits<{ sort: [string] }>();

const active = computed(() => props.sort.startsWith(`${props.field}-`));
const ascending = computed(() => props.sort.endsWith('-asc'));
const ariaSort = computed(() => (active.value ? (ascending.value ? 'ascending' : 'descending') : undefined));
const icon = computed(() => (!active.value ? 'sort' : ascending.value ? 'arrow-up' : 'arrow-down'));
</script>
