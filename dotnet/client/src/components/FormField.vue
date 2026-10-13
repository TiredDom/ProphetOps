<template>
  <div class="form-field" :class="{ 'field-wide': wide, 'has-error': !!error, 'has-note': !error && !!note }">
    <label class="form-field-label" :for="id">
      {{ label }}<span v-if="optional" class="form-field-optional">optional</span>
    </label>

    <slot :id="id" :described-by="describedBy" :invalid="!!error" />

    <p v-if="error" :id="`${id}-error`" class="field-error">
      <UiIcon name="alert" class="field-message-icon" />
      <span>{{ error }}</span>
    </p>
    <p v-else-if="note" :id="`${id}-note`" class="field-note">
      <UiIcon name="alert" class="field-message-icon" />
      <span>
        {{ note }}
        <button v-if="noteAction" type="button" class="field-note-action" @click="emit('note-action')">
          {{ noteAction }}
        </button>
      </span>
    </p>
    <p v-if="hint && !error" :id="`${id}-hint`" class="field-hint">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import UiIcon from './UiIcon.vue';

/**
 * One labelled field with its hint, its advice, and its problem, each tied to the control
 * with aria-describedby so a screen reader hears them with the field. A problem replaces the
 * hint; advice (an unusual amount, a date after today) sits above it and never blocks a save.
 */
const props = defineProps<{
  id: string;
  label: string;
  error?: string;
  hint?: string;
  note?: string;
  noteAction?: string;
  optional?: boolean;
  wide?: boolean;
}>();

const emit = defineEmits<{ 'note-action': [] }>();

const describedBy = computed(() => {
  const ids: string[] = [];
  if (props.error) ids.push(`${props.id}-error`);
  else if (props.note) ids.push(`${props.id}-note`);
  if (props.hint && !props.error) ids.push(`${props.id}-hint`);
  return ids.length ? ids.join(' ') : undefined;
});
</script>
