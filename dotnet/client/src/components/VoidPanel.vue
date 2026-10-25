<template>
  <div ref="root" class="void-panel" role="group" :aria-label="`Void this ${noun}`">
    <p class="void-lead">{{ lead }}</p>
    <FormField :id="`void-reason-${noun}`" label="Why is it being voided?" :error="error" v-slot="f">
      <input
        :id="f.id"
        ref="reasonInput"
        v-model.trim="reason"
        maxlength="160"
        :placeholder="placeholder"
        :aria-invalid="f.invalid ? 'true' : undefined"
        :aria-describedby="f.describedBy"
        @keydown.enter.prevent="submit"
      />
    </FormField>
    <div class="void-actions">
      <button class="secondary-button" type="button" :disabled="busy" @click="emit('cancel')">Keep it</button>
      <button class="danger-button" type="button" :disabled="busy || !reason" @click="submit">
        {{ busy ? 'Voiding…' : `Void ${noun}` }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue';
import FormField from './FormField.vue';

/** Asks why before a record is voided. Opens with the reason field focused. */
defineProps<{ noun: string; lead: string; placeholder: string; busy: boolean; error?: string }>();
const emit = defineEmits<{ cancel: []; confirm: [string] }>();

const root = ref<HTMLElement | null>(null);
const reasonInput = ref<HTMLInputElement | null>(null);
const reason = ref('');

function submit() {
  if (reason.value) emit('confirm', reason.value);
}

onMounted(async () => {
  await nextTick();
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  root.value?.scrollIntoView({ block: 'nearest', behavior: reduce ? 'auto' : 'smooth' });
  reasonInput.value?.focus({ preventScroll: true });
});
</script>
