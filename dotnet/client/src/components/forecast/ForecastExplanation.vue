<template>
  <section class="details-block explain-block" aria-labelledby="explain-title" :aria-busy="loading && !shown">
    <div class="explain-head">
      <h3 id="explain-title" class="explain-heading">
        <UiIcon name="forecast" class="explain-mark" />
        In plain words
      </h3>
      <span v-if="shown?.source === 'gemini'" class="explain-by">Written by Gemini</span>
    </div>

    <Skeleton v-if="loading && !shown" :rows="3" />

    <div v-else-if="error && !shown" class="explain-problem" role="alert">
      <p>{{ error }}</p>
      <button type="button" class="secondary-button" @click="loadRules">Try again</button>
    </div>

    <p v-else-if="shown && !shown.ok" class="explain-summary">{{ shown.reason }}</p>

    <template v-else-if="shown?.ok">
      <!-- Plain text only: whoever wrote it, nothing here is read as markup. -->
      <p class="explain-summary">{{ shown.summary }}</p>
      <ul v-if="shown.points.length" class="explain-points">
        <li v-for="(point, index) in shown.points" :key="index">{{ point }}</li>
      </ul>

      <p v-if="notice" class="explain-notice" role="status">{{ notice }}</p>

      <div class="explain-foot">
        <p class="explain-source">
          <template v-if="shown.source === 'gemini'">
            Worded by Gemini ({{ shown.model }}) and checked: every figure in it is one of the forecast's own.
          </template>
          <template v-else>Worded by ProphetOps from the forecast's own figures.</template>
        </p>
        <button
          v-if="shown.source === 'gemini'"
          type="button"
          class="table-link"
          @click="view = 'rules'"
        >
          Show ProphetOps' wording
        </button>
        <button v-else-if="written" type="button" class="table-link" @click="view = 'gemini'">
          Show Gemini's wording
        </button>
        <button
          v-else-if="rules?.aiAvailable"
          type="button"
          class="secondary-button"
          :disabled="asking"
          @click="askGemini"
        >
          {{ asking ? 'Asking Gemini…' : 'Word it with Gemini' }}
        </button>
      </div>

      <p v-if="rules?.aiAvailable && !written" class="explain-privacy">
        Gemini is given the agency's monthly totals, this forecast and its accuracy test, never a client, a booking or a
        member of staff. It only rewords them; Holt-Winters makes every figure.
      </p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { api, describeError, type ForecastExplanation, type ForecastMeasure } from '../../api';
import Skeleton from '../Skeleton.vue';
import UiIcon from '../UiIcon.vue';

const props = defineProps<{ measure: ForecastMeasure; active: boolean }>();

// Each measure's wording is kept while Home is open: ProphetOps' own, and Gemini's once asked for.
const own = reactive<Partial<Record<ForecastMeasure, ForecastExplanation>>>({});
const theirs = reactive<Partial<Record<ForecastMeasure, ForecastExplanation>>>({});
const notices = reactive<Partial<Record<ForecastMeasure, string>>>({});
const loading = ref(false);
const asking = ref(false);
const error = ref('');
const view = ref<'rules' | 'gemini'>('rules');
let asked = 0;

const rules = computed(() => own[props.measure] ?? null);
const written = computed(() => theirs[props.measure] ?? null);
const shown = computed(() => (view.value === 'gemini' && written.value ? written.value : rules.value));
const notice = computed(() => (view.value === 'rules' ? notices[props.measure] ?? '' : ''));

const fallbackNotices: Record<string, string> = {
  busy: 'Gemini has been asked often in the last hour, so this is ProphetOps’ own wording. Try again later.',
  unavailable: 'Gemini could not be reached or did not answer as asked, so this is ProphetOps’ own wording.',
  'unknown-figure': 'Gemini’s wording held a figure that is not in the forecast, so it was set aside. This is ProphetOps’ own wording.',
  'not-configured': 'Gemini is not set up for this agency, so this is ProphetOps’ own wording.',
};

async function loadRules() {
  const measure = props.measure;
  const ask = ++asked;
  loading.value = true;
  error.value = '';
  try {
    const answer = await api.forecastExplanation(measure);
    own[measure] = answer;
  } catch (err: unknown) {
    if (ask === asked) error.value = describeError(err, 'The forecast could not be put into words. Try again in a moment.');
  } finally {
    if (ask === asked) loading.value = false;
  }
}

async function askGemini() {
  const measure = props.measure;
  asking.value = true;
  delete notices[measure];
  try {
    const answer = await api.explainWithGemini(measure);
    if (answer.source === 'gemini') {
      theirs[measure] = answer;
      if (props.measure === measure) view.value = 'gemini';
    } else {
      notices[measure] = fallbackNotices[answer.fallbackReason ?? 'unavailable'] ?? fallbackNotices.unavailable;
    }
  } catch (err: unknown) {
    notices[measure] = describeError(err, 'Gemini could not be asked just now. ProphetOps’ own wording is shown.');
  } finally {
    asking.value = false;
  }
}

// Worded only once the details are open, like the accuracy test beside it.
watch(
  () => [props.active, props.measure] as const,
  ([active, measure], before) => {
    if (before && before[1] !== measure) view.value = theirs[measure] ? 'gemini' : 'rules';
    if (active && !own[measure]) void loadRules();
  },
  { immediate: true },
);
</script>

<style scoped>
.explain-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2) var(--space-5);
}

.explain-heading {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.explain-mark {
  color: var(--color-model);
  font-size: 18px;
}

.explain-by {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 500;
}

.explain-summary {
  max-width: 70ch;
  margin: 0;
  color: var(--color-text-primary);
  font-size: var(--text-md);
  line-height: 1.55;
}

.explain-points {
  display: grid;
  gap: 6px;
  max-width: 75ch;
  margin: 0;
  padding-left: 1.2em;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.55;
}

.explain-points li::marker {
  color: var(--color-model);
}

.explain-notice {
  max-width: 75ch;
  margin: 0;
  color: var(--color-warning-ink);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.explain-foot {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3) var(--space-5);
}

.explain-source,
.explain-privacy {
  max-width: 75ch;
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.explain-source {
  flex: 1 1 320px;
}

.explain-problem {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3) var(--space-4);
  color: var(--color-text-secondary);
  font-size: var(--text-base);
}

.explain-problem p {
  margin: 0;
}

@media (max-width: 640px) {
  .explain-summary {
    font-size: var(--text-base);
  }

  .explain-points {
    font-size: var(--text-sm);
  }
}
</style>
