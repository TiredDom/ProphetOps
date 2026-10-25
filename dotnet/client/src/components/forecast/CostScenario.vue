<template>
  <details v-if="allowed" class="content-panel scenario" :open="open" @toggle="onToggle">
    <summary class="scenario-summary">
      <span class="summary-text">
        <span class="scenario-title">What the coming months could leave after costs</span>
        <span class="summary-meta">An estimate from the revenue forecast and past spending</span>
      </span>
      <span v-if="preview" class="summary-preview">
        <span class="summary-chip"><span class="chip-label">After costs</span> {{ preview }}</span>
      </span>
      <UiIcon name="chevron-down" class="summary-chevron" />
    </summary>

    <div class="scenario-body">
      <fieldset class="assumption">
        <legend class="assumption-legend">Assume spending</legend>
        <div class="assumption-options">
          <label class="assumption-option">
            <input v-model="basis" type="radio" name="scenario-basis" value="share" />
            <span>takes a share of revenue</span>
          </label>
          <label class="assumption-option">
            <input v-model="basis" type="radio" name="scenario-basis" value="last-year" />
            <span>repeats the same month last year</span>
          </label>
        </div>

        <div v-if="basis === 'share'" class="share-field">
          <label for="scenario-share" class="share-label">Share of revenue spent</label>
          <span class="share-control" :class="{ invalid: shareProblem }">
            <input
              id="scenario-share"
              v-model="shareText"
              class="share-input"
              inputmode="decimal"
              autocomplete="off"
              :aria-invalid="shareProblem ? 'true' : undefined"
              aria-describedby="scenario-share-note"
            />
            <span class="share-unit" aria-hidden="true">%</span>
          </span>
          <button v-if="differsFromRecorded && recordedText" type="button" class="reset-button" @click="useRecorded">
            Use the recorded {{ recordedText }}%
          </button>
          <p id="scenario-share-note" class="share-note" :class="{ problem: shareProblem }">
            {{ shareProblem || shareNote }}
          </p>
        </div>
      </fieldset>

      <Skeleton v-if="loading && !data" :rows="3" />

      <div v-else-if="error && !data" class="scenario-problem" role="alert">
        <p>{{ error }}</p>
        <button type="button" class="secondary-button" @click="load">Try again</button>
      </div>

      <p v-else-if="data && !data.ok" class="scenario-lead">{{ data.reason }}</p>

      <template v-else-if="data?.ok && data.total">
        <p class="scenario-lead" aria-live="polite">
          {{ assumption }} the {{ data.months.length }} {{ data.months.length === 1 ? 'month' : 'months' }} from
          {{ firstMonth }} to {{ lastMonth }} would leave about
          <strong :class="{ loss: data.total.afterCosts < 0 }">{{ peso(data.total.afterCosts) }}</strong> after costs.
          If every month came in at the low or the high end of its forecast range:
          {{ peso(data.total.afterCostsLower) }} to {{ peso(data.total.afterCostsUpper) }}.
        </p>

        <div class="table-scroll">
          <table class="dss-table scenario-table">
            <caption class="sr-only">Revenue forecast, assumed spending and what is left after costs, by month</caption>
            <thead>
              <tr>
                <th scope="col">Month</th>
                <th scope="col" class="num">
                  <span class="label-full">Revenue forecast</span><span class="label-short">Revenue</span>
                </th>
                <th scope="col" class="num spend-col">
                  <span class="label-full">Spending, assumed</span><span class="label-short">Spending</span>
                </th>
                <th scope="col" class="num">After costs</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in data.months" :key="m.month">
                <th scope="row">
                  <span class="label-full">{{ m.month }}</span><span class="label-short">{{ shortMonth(m.month) }}</span>
                </th>
                <td class="num model-figure">
                  <span class="label-full">{{ peso(m.revenue) }}</span><span class="label-short">{{ pesoCompact(m.revenue) }}</span>
                  <span class="fold-line">spending {{ pesoCompact(m.costs) }}</span>
                </td>
                <td class="num spend-col">
                  <span class="label-full">{{ peso(m.costs) }}</span><span class="label-short">{{ pesoCompact(m.costs) }}</span>
                </td>
                <td class="num after" :class="{ loss: m.afterCosts < 0 }">
                  <strong>
                    <span class="label-full">{{ peso(m.afterCosts) }}</span><span class="label-short">{{ pesoCompact(m.afterCosts) }}</span>
                  </strong>
                  <span class="range">{{ pesoCompact(m.afterCostsLower) }} to {{ pesoCompact(m.afterCostsUpper) }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <p v-if="data.passedMonths.length" class="scenario-note">
          The forecast also covers {{ joinNames(data.passedMonths) }}, which {{ data.passedMonths.length === 1 ? 'has' : 'have' }}
          already passed and {{ data.passedMonths.length === 1 ? 'is' : 'are' }} left out. Recording the latest bookings
          brings the forecast up to date.
        </p>
        <p class="scenario-note">
          A scenario, not a forecast: it shows what the revenue forecast would leave if spending followed the assumption
          above. Revenue after costs is booking revenue less spending, not profit or cash in hand.
        </p>
      </template>
    </div>
  </details>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { api, describeError, type CostScenario } from '../../api';
import { peso, pesoCompact } from '../../format';
import { canSeeForecast } from '../../nav';
import { useAuth } from '../../stores/auth';
import Skeleton from '../Skeleton.vue';
import UiIcon from '../UiIcon.vue';

const { state: auth } = useAuth();
const allowed = computed(() => canSeeForecast(auth.user?.role));

const open = ref(false);
const basis = ref<CostScenario['basis']>('share');
const shareText = ref('');
const data = ref<CostScenario | null>(null);
const loading = ref(false);
const error = ref('');
let asked = 0;
let typingTimer: ReturnType<typeof setTimeout> | undefined;

const recordedText = computed(() =>
  data.value?.recordedShare != null ? percentText(data.value.recordedShare) : '');

/** The share as a fraction, or null while the box is empty or unreadable. */
const typedShare = computed(() => {
  const text = shareText.value.trim().replace(',', '.');
  if (!text) return null;
  const value = Number(text);
  return Number.isFinite(value) && value >= 0 && value <= 200 ? value / 100 : null;
});

const shareProblem = computed(() =>
  shareText.value.trim() && typedShare.value == null ? 'Use a share from 0 to 200%.' : '');

const differsFromRecorded = computed(() =>
  shareText.value.trim() !== '' && recordedText.value !== '' && shareText.value.trim() !== recordedText.value);

const shareNote = computed(() => {
  const period = data.value?.basisPeriod;
  if (!period || data.value?.recordedShare == null) return 'Type the share of revenue you expect spending to take.';
  return `Spending took ${recordedText.value}% of revenue from ${period.from} to ${period.to}.`;
});

const assumption = computed(() => {
  const scenario = data.value;
  if (!scenario) return '';
  if (scenario.basis === 'last-year') return 'If spending repeats what was spent in the same month a year earlier,';
  const period = scenario.basisPeriod;
  if (!differsFromRecorded.value && period) {
    return `If spending keeps taking the ${recordedText.value}% of revenue it took from ${period.from} to ${period.to},`;
  }
  return `If spending takes ${percentText(scenario.share ?? 0)}% of revenue,`;
});

const firstMonth = computed(() => data.value?.months[0]?.month ?? '');
const lastMonth = computed(() => data.value?.months[data.value.months.length - 1]?.month ?? '');

const preview = computed(() => {
  const total = data.value?.ok ? data.value.total : null;
  return total ? `about ${pesoCompact(total.afterCosts)}, ${shortMonth(firstMonth.value)} to ${shortMonth(lastMonth.value)}` : '';
});

async function load() {
  if (!allowed.value) return;
  const ask = ++asked;
  // The recorded share is the server's own starting point; a typed one replaces it.
  const share = basis.value === 'share' && differsFromRecorded.value ? typedShare.value ?? undefined : undefined;
  loading.value = true;
  error.value = '';
  try {
    const result = await api.costScenario(basis.value, share);
    if (ask !== asked) return;
    data.value = result;
    if (!shareText.value.trim() && result.recordedShare != null) shareText.value = percentText(result.recordedShare);
  } catch (err: unknown) {
    if (ask !== asked) return;
    error.value = describeError(err, 'The scenario could not be worked out. Try again in a moment.');
    data.value = null;
  } finally {
    if (ask === asked) loading.value = false;
  }
}

function useRecorded() {
  shareText.value = recordedText.value;
}

function onToggle(event: Event) {
  open.value = (event.target as HTMLDetailsElement).open;
}

watch(basis, () => void load());

watch(shareText, (text, before) => {
  // Filling in the recorded share after the first answer is not a change to send back.
  if (!before && text === recordedText.value) return;
  clearTimeout(typingTimer);
  if (shareProblem.value) return;
  typingTimer = setTimeout(() => void load(), 350);
});

onMounted(() => void load());
onBeforeUnmount(() => clearTimeout(typingTimer));

function percentText(share: number): string {
  return (Math.round(share * 1000) / 10).toFixed(1).replace(/\.0$/, '');
}

/** "October 2026" becomes "Oct 2026" for narrow screens and the summary. */
function shortMonth(label: string): string {
  const [name, year] = label.split(' ');
  return year ? `${name.slice(0, 3)} ${year}` : label;
}

function joinNames(names: string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}
</script>

<style scoped>
.scenario {
  padding-top: 0;
  padding-bottom: 0;
}

.scenario[open] {
  padding-bottom: 24px;
}

.scenario-summary {
  display: flex;
  align-items: center;
  gap: 14px;
  min-height: 64px;
  margin: 0 -24px;
  padding: 12px 24px;
  border-radius: var(--radius-lg);
  cursor: pointer;
  list-style: none;
  transition: background var(--transition-fast);
}

.scenario-summary::-webkit-details-marker {
  display: none;
}

.scenario-summary:hover {
  background: var(--color-surface-hover);
}

.scenario-summary:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: -2px;
}

.scenario[open] .scenario-summary {
  margin-bottom: 16px;
  border-bottom: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-lg) var(--radius-lg) 0 0;
}

.summary-text {
  display: grid;
  flex: 1 1 0;
  gap: 1px;
  min-width: 0;
}

.scenario-title {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.summary-meta {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.summary-preview {
  display: flex;
  justify-content: flex-end;
}

.summary-chip {
  padding: 3px 10px;
  border-radius: var(--radius-pill);
  background: var(--color-model-soft);
  color: var(--color-model-ink);
  font-size: var(--text-xs);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.chip-label {
  color: var(--color-text-muted);
  font-weight: 500;
}

.summary-chevron {
  color: var(--color-text-muted);
  font-size: 20px;
  transition: transform var(--transition-normal);
}

.scenario[open] .summary-chevron {
  transform: rotate(180deg);
}

.scenario-body {
  display: grid;
  gap: 16px;
}

.assumption {
  display: grid;
  gap: 10px;
  margin: 0;
  padding: 0;
  border: 0;
  min-width: 0;
}

.assumption-legend {
  margin-bottom: 6px;
  padding: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 500;
}

.assumption-options {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 20px;
}

.assumption-option {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 32px;
  color: var(--color-text-primary);
  font-size: var(--text-base);
  cursor: pointer;
}

.assumption-option input {
  width: 16px;
  height: 16px;
  margin: 0;
  accent-color: var(--color-primary);
}

.share-field {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 12px;
}

.share-label {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 500;
}

.share-control {
  display: inline-flex;
  align-items: center;
  height: 38px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.share-control:hover {
  border-color: var(--color-text-faint);
}

.share-control:focus-within {
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.share-control.invalid {
  border-color: var(--color-danger);
}

.share-input {
  width: 64px;
  height: 100%;
  min-height: 0;
  padding: 0 4px 0 10px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  font: inherit;
  font-size: var(--text-base);
  font-variant-numeric: lining-nums tabular-nums;
  text-align: right;
  outline: none;
}

/* The box around the field carries the focus ring, so the field itself does not draw a second. */
.share-input:focus {
  box-shadow: none;
}

.share-input[aria-invalid='true'] {
  color: var(--color-danger-ink);
}

.share-unit {
  padding-right: 10px;
  color: var(--color-text-muted);
}

.reset-button {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.reset-button:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.reset-button:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-radius: var(--radius-sm);
}

.share-note {
  flex-basis: 100%;
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.share-note.problem {
  color: var(--color-danger-ink);
}

.scenario-problem {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px 16px;
  color: var(--color-text-secondary);
}

.scenario-problem p {
  margin: 0;
}

.scenario-lead {
  max-width: 75ch;
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.55;
}

.scenario-lead strong {
  color: var(--color-text-primary);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.scenario-lead strong.loss {
  color: var(--color-danger-ink);
}

.scenario-table {
  min-width: 0;
}

.scenario-table tbody th {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 500;
  border-bottom: 1px solid var(--color-border-subtle);
}

.scenario-table td.model-figure {
  color: var(--color-model-ink);
}

.scenario-table td.after strong {
  display: block;
  color: var(--color-text-primary);
}

.scenario-table td.after.loss strong {
  color: var(--color-danger-ink);
}

.range {
  display: block;
  margin-top: 1px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
}

.label-short,
.fold-line {
  display: none;
}

.scenario-note {
  max-width: 75ch;
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

@media (max-width: 760px) {
  .scenario-summary {
    flex-wrap: wrap;
    margin: 0 -16px;
    padding: 12px 16px;
  }

  .summary-preview {
    order: 3;
    flex-basis: 100%;
    justify-content: flex-start;
  }
}

@media (max-width: 640px) {
  .label-full {
    display: none;
  }

  .label-short {
    display: inline;
  }

  .scenario-table th,
  .scenario-table td {
    padding: 10px 6px;
  }

  .scenario-table th:first-child {
    padding-left: 0;
  }

  .scenario-table th:last-child,
  .scenario-table td:last-child {
    padding-right: 0;
  }

  /* The range may take two lines so the four columns keep to a phone's width. */
  .range {
    white-space: normal;
  }

  .scenario-lead {
    font-size: var(--text-sm);
  }

  .share-control {
    height: 44px;
  }

  .share-input {
    font-size: 16px;
  }
}

/* The narrowest phones: the assumed spending goes under the revenue it is set against. */
@media (max-width: 380px) {
  .scenario-table .spend-col {
    display: none;
  }

  .fold-line {
    display: block;
    margin-top: 1px;
    color: var(--color-text-muted);
    font-size: var(--text-xs);
    font-weight: 400;
  }
}
</style>
