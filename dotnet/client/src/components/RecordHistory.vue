<template>
  <section class="record-history" :aria-labelledby="headingId">
    <h3 :id="headingId" class="record-history-title">Change history</h3>

    <Skeleton v-if="loading" :rows="3" />

    <p v-else-if="error" class="record-history-note">
      {{ error }}
      <button type="button" class="table-link" @click="load">Try again</button>
    </p>

    <p v-else-if="items.length === 0" class="record-history-note">No changes have been recorded for this {{ noun }}.</p>

    <ol v-else class="history">
      <li v-for="item in items" :key="item.key" class="history-item">
        <span class="history-mark" :class="`kind-${item.kind}`" aria-hidden="true">
          <UiIcon :name="item.icon" :stroke-width="2.4" />
        </span>
        <div class="history-body">
          <p class="history-line">
            <span class="history-verb">{{ item.verb }}</span> <span class="history-actor">by {{ item.actor }}</span>
          </p>
          <ul v-if="item.changes.length" class="history-changes">
            <li v-for="change in item.changes" :key="change.field + change.after">
              <span class="change-field">{{ change.field }}</span> <span class="change-before">{{ change.before }}</span> <UiIcon name="arrow-right" class="change-arrow" /><span class="visually-hidden">to</span> <span class="change-after">{{ change.after }}</span>
            </li>
          </ul>
          <p v-else-if="item.note" class="history-note">{{ item.note }}</p>
        </div>
        <time class="history-time" :datetime="item.at" :title="item.absolute">{{ item.when }}</time>
      </li>
    </ol>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { api, type ActivityEntry } from '../api';
import { peso } from '../format';
import { formatDate, isIsoDate } from '../periods';
import Skeleton from './Skeleton.vue';
import UiIcon, { type UiIconName } from './UiIcon.vue';

const props = withDefaults(defineProps<{ entityType: string; code: string; version?: number }>(), { version: 0 });

const headingId = `history-${props.entityType}-${props.code}`.replace(/[^\w-]/g, '-');

const entries = ref<ActivityEntry[]>([]);
const loading = ref(true);
const error = ref('');

const nouns: Record<string, string> = { Booking: 'booking', Expense: 'expense', TravelPackage: 'package', User: 'account' };
const noun = computed(() => nouns[props.entityType] ?? 'record');

type Kind = 'created' | 'updated' | 'voided' | 'restored' | 'imported';

const kinds: Record<string, { kind: Kind; icon: UiIconName; verb: string }> = {
  Created: { kind: 'created', icon: 'plus', verb: 'Created' },
  Updated: { kind: 'updated', icon: 'pencil', verb: 'Changed' },
  Voided: { kind: 'voided', icon: 'void', verb: 'Voided' },
  Restored: { kind: 'restored', icon: 'restore', verb: 'Restored' },
  Imported: { kind: 'imported', icon: 'upload', verb: 'Imported' },
};

// Field names the server writes into an edit's summary, longest first so "Related package"
// is not read as a field called "Related".
const fields = [
  'Related package', 'Passengers', 'Destination', 'Inclusions', 'Available', 'Category', 'Duration',
  'Reserved', 'Revenue', 'Payment', 'Amount', 'Client', 'Status', 'Notes', 'Price', 'Name', 'Role', 'Sold', 'Date',
];
const moneyFields = new Set(['Revenue', 'Amount', 'Price']);

interface Change {
  field: string;
  before: string;
  after: string;
}

function show(field: string, value: string): string {
  const text = value.trim();
  if (moneyFields.has(field) && /^\d+$/.test(text)) return peso(Number(text));
  if (isIsoDate(text)) return formatDate(text);
  return text;
}

/** "Payment Pending → Paid; Revenue 12000 → 15000" becomes one readable line per field. */
function changesOf(summary: string): Change[] {
  return summary
    .split('; ')
    .map((part) => part.replace(/ \(bulk\)$/, ''))
    .map((part): Change | null => {
      const field = fields.find((name) => part.startsWith(`${name} `));
      const arrow = part.indexOf(' → ');
      if (!field || arrow < 0) return null;
      return {
        field,
        before: show(field, part.slice(field.length + 1, arrow)),
        after: show(field, part.slice(arrow + 3)),
      };
    })
    .filter((change): change is Change => change !== null);
}

/** Audit times are stored in UTC; older rows can arrive without the zone marker. */
function parseAt(iso: string): Date {
  return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
}

const sameYear = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
const otherYear = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
const full = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' });

const items = computed(() =>
  entries.value.map((entry, index) => {
    const at = parseAt(entry.at);
    const mapped = kinds[entry.action] ?? { kind: 'updated' as Kind, icon: 'pencil' as UiIconName, verb: entry.action };
    // Summaries written when a record is created spell the peso sign as "P".
    const summary = (entry.summary ?? '').replace(/(^|, )P(?=\d)/g, '$1₱');
    const changes = entry.action === 'Updated' ? changesOf(summary) : [];
    let note = summary;
    if (entry.action === 'Voided' && summary) note = `Reason: ${summary}`;
    if (entry.action === 'Updated' && changes.length === 0) note = summary;
    const valid = !Number.isNaN(at.getTime());
    return {
      key: `${entry.at}-${entry.action}-${index}`,
      at: entry.at,
      kind: mapped.kind,
      icon: mapped.icon,
      verb: mapped.verb,
      actor: entry.actorName || entry.actor,
      changes,
      note,
      when: !valid ? '' : at.getFullYear() === new Date().getFullYear() ? sameYear.format(at) : otherYear.format(at),
      absolute: valid ? full.format(at) : '',
    };
  }),
);

async function load() {
  loading.value = true;
  error.value = '';
  try {
    entries.value = await api.activity(props.entityType, props.code, 50);
  } catch {
    error.value = 'The history could not be loaded.';
  } finally {
    loading.value = false;
  }
}

watch(() => [props.entityType, props.code, props.version], load, { immediate: true });
</script>

<style scoped>
.record-history {
  display: grid;
  gap: 12px;
}

.record-history-title {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.record-history-note {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.history {
  margin: 0;
  padding: 0;
  list-style: none;
}

.history-item {
  position: relative;
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr) auto;
  gap: 2px 12px;
  align-items: start;
  padding-bottom: 16px;
}

/* One line runs down the left, joining each change to the one before it. */
.history-item:not(:last-child)::before {
  content: '';
  position: absolute;
  top: 28px;
  bottom: 4px;
  left: 11.5px;
  width: 1px;
  background: var(--color-border-strong);
}

.history-mark {
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  color: var(--color-on-shell);
  font-size: 12px;
}

.kind-created {
  background: var(--color-success);
}

.kind-updated,
.kind-imported {
  background: var(--color-primary);
}

.kind-voided {
  background: var(--color-danger);
}

.kind-restored {
  background: var(--color-warning);
}

.history-body {
  display: grid;
  gap: 4px;
  min-width: 0;
  padding-top: 2px;
}

.history-line {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.history-verb {
  color: var(--color-text-primary);
  font-weight: 600;
}

.history-changes {
  display: grid;
  gap: 3px;
  margin: 0;
  padding: 0;
  list-style: none;
  font-size: var(--text-sm);
}

.history-changes li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 2px 6px;
  min-width: 0;
}

.change-field {
  color: var(--color-text-muted);
}

.change-before {
  color: var(--color-text-muted);
  text-decoration: line-through;
  text-decoration-color: var(--color-text-faint);
  overflow-wrap: anywhere;
}

.change-arrow {
  color: var(--color-text-faint);
  font-size: 12px;
}

.change-after {
  color: var(--color-text-primary);
  font-weight: 600;
  overflow-wrap: anywhere;
}

.history-note {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  overflow-wrap: anywhere;
}

.history-time {
  padding-top: 2px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}
</style>
