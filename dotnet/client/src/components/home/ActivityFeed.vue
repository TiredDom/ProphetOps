<template>
  <section class="content-panel feed-panel" aria-labelledby="activity-title">
    <div class="feed-head">
      <h2 id="activity-title" class="feed-heading">Recent activity</h2>
      <span class="live-status" :class="connection">
        <span class="live-dot" aria-hidden="true"></span>
        <span class="live-label">{{ connection === 'live' ? 'Live' : 'Reconnecting' }}</span>
        <span v-if="lastUpdated" class="live-updated">· updated {{ sinceUpdate }}</span>
      </span>
    </div>

    <p class="visually-hidden" aria-live="polite">{{ announcement }}</p>

    <Skeleton v-if="loading" :rows="4" />

    <div v-else-if="entries.length === 0" class="feed-empty">
      <p v-if="error">
        {{ error }}
        <button type="button" class="feed-retry" @click="refresh()">Try again</button>
      </p>
      <p v-else>No changes yet. New bookings, edits and voided records will appear here as they happen.</p>
    </div>

    <div v-else class="feed">
      <section v-for="group in groups" :key="group.label" class="feed-day" :aria-label="group.label">
        <h3 class="feed-day-label">{{ group.label }}</h3>
        <ol class="feed-list">
          <li
            v-for="item in group.items"
            :key="item.key"
            class="feed-item"
            :class="{ fresh: fresh.has(item.key) }"
          >
            <span class="feed-avatar" aria-hidden="true">
              {{ item.initials }}
              <span class="feed-badge" :class="`kind-${item.kind}`">
                <UiIcon :name="item.icon" :stroke-width="2.6" />
              </span>
            </span>
            <div class="feed-body">
              <p class="feed-line">
                <span class="feed-actor">{{ item.actor }}</span>
                {{ item.verb }} {{ item.noun }}
                <span class="feed-code">{{ item.entry.entityCode }}</span>
              </p>
              <p v-if="item.entry.summary" class="feed-summary" :title="item.entry.summary">{{ item.entry.summary }}</p>
            </div>
            <time class="feed-time" :datetime="item.entry.at" :title="item.absolute">{{ item.time }}</time>
          </li>
        </ol>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { api, type ActivityEntry } from '../../api';
import Skeleton from '../Skeleton.vue';
import UiIcon, { type UiIconName } from '../UiIcon.vue';

const props = withDefaults(defineProps<{ limit?: number; refreshSeconds?: number }>(), {
  limit: 6,
  refreshSeconds: 30,
});

const emit = defineEmits<{ changed: [] }>();

const entries = ref<ActivityEntry[]>([]);
const loading = ref(true);
const error = ref('');
const connection = ref<'live' | 'retrying'>('live');
const lastUpdated = ref<Date | null>(null);
const now = ref(new Date());
const fresh = ref(new Set<string>());
const announcement = ref('');
let timer: ReturnType<typeof setInterval> | undefined;
let freshTimer: ReturnType<typeof setTimeout> | undefined;

type Kind = 'created' | 'updated' | 'voided' | 'restored' | 'imported' | 'other';

const kinds: Record<string, { kind: Kind; icon: UiIconName }> = {
  Created: { kind: 'created', icon: 'plus' },
  Updated: { kind: 'updated', icon: 'pencil' },
  Voided: { kind: 'voided', icon: 'void' },
  Restored: { kind: 'restored', icon: 'restore' },
  Imported: { kind: 'imported', icon: 'upload' },
};

const nouns: Record<string, string> = {
  Booking: 'booking',
  Expense: 'expense',
  TravelPackage: 'package',
  User: 'account',
};

function keyOf(entry: ActivityEntry): string {
  return `${entry.at}|${entry.action}|${entry.entityType}|${entry.entityCode}`;
}

/** Audit times are stored in UTC; older rows can arrive without the zone marker. */
function parseAt(iso: string): Date {
  return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
}

function initialsOf(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || '?';
}

const timeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' });
const dayFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' });
const dayYearFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
const fullFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' });

function startOfDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

function dayLabel(at: Date): string {
  const days = Math.round((startOfDay(now.value) - startOfDay(at)) / 86_400_000);
  if (days === 0) return 'Today';
  if (days === 1) return 'Yesterday';
  return at.getFullYear() === now.value.getFullYear() ? dayFormat.format(at) : dayYearFormat.format(at);
}

/** Today reads as elapsed time; earlier days already carry the date in their heading. */
function timeLabel(at: Date): string {
  if (startOfDay(at) !== startOfDay(now.value)) return timeFormat.format(at);
  const minutes = Math.floor((now.value.getTime() - at.getTime()) / 60_000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes} min ago`;
  return `${Math.floor(minutes / 60)} h ago`;
}

const groups = computed(() => {
  const result: { label: string; items: ReturnType<typeof present>[] }[] = [];
  for (const entry of entries.value) {
    const item = present(entry);
    const last = result[result.length - 1];
    if (last && last.label === item.day) last.items.push(item);
    else result.push({ label: item.day, items: [item] });
  }
  return result;
});

function present(entry: ActivityEntry) {
  const at = parseAt(entry.at);
  const mapped = kinds[entry.action] ?? { kind: 'other' as Kind, icon: 'pencil' as UiIconName };
  const actor = entry.actorName || entry.actor;
  return {
    key: keyOf(entry),
    entry,
    actor,
    initials: initialsOf(actor),
    kind: mapped.kind,
    icon: mapped.icon,
    verb: entry.action.toLowerCase(),
    noun: nouns[entry.entityType] ?? entry.entityType.toLowerCase(),
    day: dayLabel(at),
    time: timeLabel(at),
    absolute: Number.isNaN(at.getTime()) ? '' : fullFormat.format(at),
  };
}

const sinceUpdate = computed(() => {
  if (!lastUpdated.value) return '';
  const seconds = Math.floor((now.value.getTime() - lastUpdated.value.getTime()) / 1000);
  if (seconds < 45) return 'just now';
  const minutes = Math.round(seconds / 60);
  return `${minutes} min ago`;
});

async function refresh(initial = false) {
  try {
    const next = await api.activity(undefined, undefined, props.limit);
    if (!initial) {
      const known = new Set(entries.value.map(keyOf));
      const added = next.filter((entry) => !known.has(keyOf(entry)));
      if (added.length > 0) markFresh(added);
    }
    entries.value = next;
    lastUpdated.value = new Date();
    connection.value = 'live';
    error.value = '';
  } catch {
    connection.value = 'retrying';
    if (entries.value.length === 0) error.value = 'Recent activity could not be loaded.';
  } finally {
    loading.value = false;
    now.value = new Date();
  }
}

function markFresh(added: ActivityEntry[]) {
  fresh.value = new Set(added.map(keyOf));
  const first = present(added[0]);
  announcement.value = added.length === 1
    ? `New activity: ${first.actor} ${first.verb} ${first.noun} ${added[0].entityCode}.`
    : `${added.length} new activity entries.`;
  if (freshTimer) clearTimeout(freshTimer);
  freshTimer = setTimeout(() => {
    fresh.value = new Set();
  }, 2600);
  emit('changed');
}

function tick() {
  now.value = new Date();
  if (document.visibilityState === 'visible') void refresh();
}

function onVisibility() {
  if (document.visibilityState === 'visible') void refresh();
}

onMounted(() => {
  void refresh(true);
  timer = setInterval(tick, props.refreshSeconds * 1000);
  document.addEventListener('visibilitychange', onVisibility);
});

onBeforeUnmount(() => {
  if (timer) clearInterval(timer);
  if (freshTimer) clearTimeout(freshTimer);
  document.removeEventListener('visibilitychange', onVisibility);
});
</script>

<style scoped>
.feed-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 6px 16px;
  margin-bottom: 18px;
}

.feed-heading {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

/* Live status: a steady dot that breathes while updates are flowing */
.live-status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.live-label {
  color: var(--color-success-ink);
  font-weight: 600;
}

.live-dot {
  position: relative;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--color-success);
}

.live-dot::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: var(--color-success);
  animation: live-breathe 2.4s var(--ease-out) infinite;
}

.retrying .live-label {
  color: var(--color-warning-ink);
}

.retrying .live-dot,
.retrying .live-dot::after {
  background: var(--color-warning);
  animation: none;
}

@keyframes live-breathe {
  0% {
    transform: scale(1);
    opacity: 0.55;
  }

  70%,
  100% {
    transform: scale(2.6);
    opacity: 0;
  }
}

/* Feed: days as headings, each change hanging off one timeline */
.feed {
  display: grid;
  gap: 18px;
}

.feed-day-label {
  margin-bottom: 10px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  font-weight: 600;
}

.feed-list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.feed-item {
  position: relative;
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr) auto;
  gap: 2px 12px;
  align-items: start;
  margin: 0 -8px;
  padding: 8px 8px 14px;
  border-radius: var(--radius-md);
}

/* The connecting line runs from one avatar to the next within a day. */
.feed-item:not(:last-child)::before {
  content: '';
  position: absolute;
  top: 42px;
  bottom: -6px;
  left: 23.5px;
  width: 1px;
  background: var(--color-border-strong);
}

.feed-item.fresh {
  animation: feed-arrive 2.6s var(--ease-out);
}

@keyframes feed-arrive {
  0% {
    background: var(--tint-primary);
    transform: translateY(-6px);
    opacity: 0;
  }

  12% {
    background: var(--tint-primary);
    transform: none;
    opacity: 1;
  }

  100% {
    background: transparent;
  }
}

.feed-avatar {
  position: relative;
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--color-shell);
  color: #FFFFFF;
  font-size: var(--text-xs);
  font-weight: 600;
}

.feed-badge {
  position: absolute;
  right: -5px;
  bottom: -4px;
  display: grid;
  place-items: center;
  width: 17px;
  height: 17px;
  border: 2px solid var(--color-surface);
  border-radius: 50%;
  color: #FFFFFF;
  font-size: 9px;
}

.kind-created {
  background: var(--color-success);
}

.kind-updated,
.kind-imported,
.kind-other {
  background: var(--color-primary);
}

.kind-voided {
  background: var(--color-danger);
}

.kind-restored {
  background: var(--color-warning);
}

.feed-body {
  min-width: 0;
  padding-top: 1px;
}

.feed-line {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  line-height: 1.45;
}

.feed-actor {
  color: var(--color-text-primary);
  font-weight: 600;
}

.feed-code {
  color: var(--color-text-primary);
  font-weight: 500;
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.feed-summary {
  margin-top: 2px;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.feed-time {
  padding-top: 2px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.feed-empty {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.feed-retry {
  margin-left: 6px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

@media (prefers-reduced-motion: reduce) {
  .live-dot::after,
  .feed-item.fresh {
    animation: none;
  }

  .feed-item.fresh {
    background: var(--tint-primary);
  }
}
</style>
