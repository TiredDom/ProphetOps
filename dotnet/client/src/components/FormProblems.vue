<template>
  <div v-if="problems.length > 1" class="form-problems" role="alert">
    <p class="form-problems-title">{{ problems.length }} fields need a change before this can be saved</p>
    <ul class="form-problems-list">
      <li v-for="problem in problems" :key="problem.key">
        <button type="button" class="form-problems-link" @click="emit('focus', problem.key)">{{ problem.label }}</button>
        <span>{{ problem.message }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts" generic="K extends string">
import type { FieldProblem } from '../composables/useFieldErrors';

/**
 * After a save attempt with more than one problem, names each one with a link to its field.
 * A single problem needs no list: focus goes straight to the field, where its message is.
 */
defineProps<{ problems: FieldProblem<K>[] }>();
const emit = defineEmits<{ focus: [K] }>();
</script>
