<template>
  <section class="content-panel month-panel" aria-labelledby="month-title">
    <div class="month-head">
      <h2 id="month-title" class="month-heading">This month so far</h2>
      <span class="month-meta">{{ rangeLabel(month.current) }} · day {{ dayOfMonth }} of {{ daysInMonth }}</span>
    </div>

    <div v-if="empty" class="month-empty">
      <span class="month-empty-mark" aria-hidden="true"><UiIcon name="calendar" /></span>
      <div class="month-empty-body">
        <p class="month-empty-title">Nothing recorded yet in {{ monthName }}</p>
        <p class="month-empty-text">
          Bookings and expenses dated from {{ monthName }} 1 are counted here and compared with the same days in
          {{ previousMonthName }}.
        </p>
        <div class="month-empty-actions">
          <RouterLink class="secondary-button compact-button" :to="{ path: '/bookings', query: { new: '1' } }">
            <UiIcon name="plus" /> Record a booking
          </RouterLink>
          <RouterLink class="secondary-button compact-button" :to="{ path: '/expenses', query: { new: '1' } }">
            <UiIcon name="plus" /> Add an expense
          </RouterLink>
        </div>
      </div>
    </div>

    <template v-else>
      <div class="score-grid">
        <article v-for="metric in metrics" :key="metric.key" class="score">
          <h3 class="score-label">{{ metric.label }}</h3>
          <p class="score-value">{{ metric.show(metric.current) }}</p>
          <p class="score-change" :class="metric.tone">
            <UiIcon v-if="metric.direction" :name="metric.direction" class="score-change-mark" />
            {{ metric.change }}
          </p>
          <div class="score-bars" role="img" :aria-label="metric.barsLabel">
            <span class="bar-label">{{ previousShort }}</span>
            <span class="bar-track"><span class="bar-fill prior" :style="{ width: metric.priorWidth }"></span></span>
            <span class="bar-value">{{ metric.compact(metric.previous) }}</span>
            <span class="bar-label">{{ currentShort }}</span>
            <span class="bar-track"><span class="bar-fill" :style="{ width: metric.currentWidth }"></span></span>
            <span class="bar-value current">{{ metric.compact(metric.current) }}</span>
          </div>
        </article>
      </div>
      <p class="month-footnote">
        Compared with {{ rangeLabel(month.previous) }}. Revenue and expenses exclude voided records; bookings count by
        booking date.
      </p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PeriodTotals } from '../../api';
import { count, peso, pesoCompact } from '../../format';
import UiIcon, { type UiIconName } from '../UiIcon.vue';

const props = defineProps<{ month: { current: PeriodTotals; previous: PeriodTotals } }>();

function parseDay(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

const monthLong = new Intl.DateTimeFormat('en-US', { month: 'long' });
const monthShort = new Intl.DateTimeFormat('en-US', { month: 'short' });
const dayFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' });

const currentEnd = computed(() => parseDay(props.month.current.to));
const previousStart = computed(() => parseDay(props.month.previous.from));

const dayOfMonth = computed(() => currentEnd.value.getDate());
const daysInMonth = computed(() => new Date(currentEnd.value.getFullYear(), currentEnd.value.getMonth() + 1, 0).getDate());
const monthName = computed(() => monthLong.format(currentEnd.value));
const previousMonthName = computed(() => monthLong.format(previousStart.value));
const currentShort = computed(() => monthShort.format(currentEnd.value));
const previousShort = computed(() => monthShort.format(previousStart.value));

/** "Oct 1–13", or "Oct 1" for a single day. */
function rangeLabel(period: PeriodTotals): string {
  const from = parseDay(period.from);
  if (period.from === period.to) return dayFormat.format(from);
  return `${dayFormat.format(from)}–${parseDay(period.to).getDate()}`;
}

const empty = computed(() =>
  [props.month.current, props.month.previous].every((p) => p.revenue === 0 && p.bookings === 0 && p.expenses === 0));

type Metric = {
  key: 'revenue' | 'bookings' | 'expenses';
  label: string;
  current: number;
  previous: number;
  show: (value: number) => string;
  compact: (value: number) => string;
  /** Expenses going up is not good or bad on its own, so it carries no colour. */
  judged: boolean;
};

const metrics = computed(() => {
  const { current, previous } = props.month;
  const base: Metric[] = [
    { key: 'revenue', label: 'Booking revenue', current: current.revenue, previous: previous.revenue, show: peso, compact: pesoCompact, judged: true },
    { key: 'bookings', label: 'Bookings', current: current.bookings, previous: previous.bookings, show: count, compact: count, judged: true },
    { key: 'expenses', label: 'Expenses recorded', current: current.expenses, previous: previous.expenses, show: peso, compact: pesoCompact, judged: false },
  ];

  return base.map((m) => {
    const max = Math.max(m.current, m.previous, 1);
    const width = (value: number) => `${value > 0 ? Math.max(3, Math.round((value / max) * 100)) : 0}%`;
    let change = 'None in the same days last month';
    let direction: UiIconName | null = null;
    let tone = '';

    if (m.previous === 0 && m.current === 0) change = 'None in either period';
    else if (m.previous > 0) {
      const pct = ((m.current - m.previous) / m.previous) * 100;
      if (Math.abs(pct) < 0.5) change = 'About the same as last month';
      else {
        const size = Math.abs(pct) >= 10 ? Math.round(Math.abs(pct)) : Math.abs(pct).toFixed(1);
        change = `${size}% ${pct > 0 ? 'more' : 'less'} than last month`;
        direction = pct > 0 ? 'up' : 'down';
        if (m.judged) tone = pct > 0 ? 'up' : 'down';
      }
    }

    return {
      ...m,
      change,
      direction,
      tone,
      priorWidth: width(m.previous),
      currentWidth: width(m.current),
      barsLabel: `${previousShort.value} ${m.show(m.previous)}, ${currentShort.value} ${m.show(m.current)}`,
    };
  });
});
</script>

<style scoped>
.month-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 4px 16px;
  margin-bottom: 18px;
}

.month-heading {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.month-meta {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-variant-numeric: lining-nums tabular-nums;
}

/* Scoreboard: the figure, how it moved, and the two periods side by side */
.score-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.score {
  display: grid;
  gap: 6px;
  align-content: start;
  min-width: 0;
  padding: 0 28px;
  border-left: 1px solid var(--color-border-subtle);
}

.score:first-child {
  padding-left: 0;
  border-left: 0;
}

.score:last-child {
  padding-right: 0;
}

.score-label {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 500;
}

.score-value {
  color: var(--color-text-primary);
  font-size: var(--text-xl);
  font-weight: 650;
  letter-spacing: -0.02em;
  line-height: 1.15;
  font-variant-numeric: lining-nums tabular-nums;
  overflow-wrap: anywhere;
}

.score-change {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.score-change-mark {
  font-size: 11px;
}

.score-change.up {
  color: var(--color-success-ink);
}

.score-change.down {
  color: var(--color-danger-ink);
}

.score-bars {
  display: grid;
  grid-template-columns: 30px minmax(0, 1fr) auto;
  align-items: center;
  gap: 6px 10px;
  margin-top: 8px;
}

.bar-label {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.bar-track {
  height: 10px;
}

.bar-fill {
  display: block;
  height: 100%;
  border-radius: 2px;
  background: var(--color-primary);
}

.bar-fill.prior {
  background: var(--color-border-strong);
}

.bar-value {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  text-align: right;
  font-variant-numeric: lining-nums tabular-nums;
}

.bar-value.current {
  color: var(--color-text-primary);
  font-weight: 600;
}

.month-footnote {
  margin-top: 18px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

/* Empty month: say what will appear here and offer the two ways to start it */
.month-empty {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.month-empty-mark {
  display: grid;
  place-items: center;
  flex: none;
  width: 44px;
  height: 44px;
  border-radius: var(--radius-md);
  background: var(--color-primary-soft);
  color: var(--color-primary);
  font-size: 22px;
}

.month-empty-body {
  display: grid;
  gap: 4px;
  justify-items: start;
}

.month-empty-title {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.month-empty-text {
  max-width: 62ch;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.month-empty-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.month-empty-actions .ui-icon {
  font-size: 15px;
}

@media (max-width: 760px) {
  .score-grid {
    grid-template-columns: minmax(0, 1fr);
    gap: 18px;
  }

  .score {
    padding: 18px 0 0;
    border-left: 0;
    border-top: 1px solid var(--color-border-subtle);
  }

  .score:first-child {
    padding-top: 0;
    border-top: 0;
  }

  .score-value {
    font-size: var(--text-lg);
  }

  .month-empty {
    flex-direction: column;
    gap: 12px;
  }
}
</style>
