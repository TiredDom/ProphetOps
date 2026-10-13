<template>
  <section class="content-panel inbox-panel" aria-labelledby="attention-title">
    <div class="inbox-head">
      <h2 id="attention-title" class="inbox-heading">Needs attention</h2>
      <span v-if="openCount > 0" class="inbox-open">{{ openCount }} to review</span>
    </div>

    <div v-if="openCount === 0" class="all-clear">
      <span class="all-clear-mark" aria-hidden="true"><UiIcon name="check" :stroke-width="2.4" /></span>
      <div>
        <p class="all-clear-title">All clear</p>
        <p class="all-clear-text">No unpaid bookings, and every package has slots available.</p>
      </div>
    </div>

    <ul v-else class="inbox">
      <li>
        <RouterLink
          v-if="unpaid.count > 0"
          class="inbox-row tone-warning"
          :to="{ path: '/bookings', query: { payment: 'unpaid' } }"
        >
          <span class="inbox-mark" aria-hidden="true"><UiIcon name="wallet" /></span>
          <span class="inbox-main">
            <span class="inbox-title">{{ unpaid.count }} unpaid {{ unpaid.count === 1 ? 'booking' : 'bookings' }}</span>
            <span class="inbox-detail">Partly paid or awaiting payment</span>
          </span>
          <span class="inbox-figure">
            <strong>{{ peso(unpaid.amount) }}</strong>
            <span>booked value</span>
          </span>
          <UiIcon name="chevron-right" class="inbox-chevron" />
        </RouterLink>
        <div v-else class="inbox-row settled">
          <span class="inbox-mark" aria-hidden="true"><UiIcon name="check" :stroke-width="2.2" /></span>
          <span class="inbox-main"><span class="inbox-title">No unpaid bookings</span></span>
        </div>
      </li>

      <li>
        <RouterLink
          v-if="low.length > 0"
          class="inbox-row"
          :class="hasCritical ? 'tone-danger' : 'tone-warning'"
          :to="{ path: '/packages', query: { stock: 'low' } }"
        >
          <span class="inbox-mark" aria-hidden="true"><UiIcon name="ticket" /></span>
          <span class="inbox-main">
            <span class="inbox-title">{{ low.length }} {{ low.length === 1 ? 'package' : 'packages' }} low on slots</span>
            <span class="slot-chips">
              <span
                v-for="p in shown"
                :key="p.code"
                class="slot-chip"
                :class="{ critical: p.status === 'Critical' }"
              >
                <span class="slot-chip-name">{{ p.packageName }}</span>
                <span class="slot-chip-count">{{ p.availableSlots }} left</span>
              </span>
              <span v-if="low.length > shown.length" class="slot-more">+{{ low.length - shown.length }} more</span>
            </span>
          </span>
          <UiIcon name="chevron-right" class="inbox-chevron" />
        </RouterLink>
        <div v-else class="inbox-row settled">
          <span class="inbox-mark" aria-hidden="true"><UiIcon name="check" :stroke-width="2.2" /></span>
          <span class="inbox-main"><span class="inbox-title">Every package has slots available</span></span>
        </div>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { LowStockPackage, PendingPayments } from '../../api';
import { peso } from '../../format';
import UiIcon from '../UiIcon.vue';

const props = defineProps<{ unpaid: PendingPayments; low: LowStockPackage[] }>();

const SHOWN = 3;

const shown = computed(() => props.low.slice(0, SHOWN));
const hasCritical = computed(() => props.low.some((p) => p.status === 'Critical'));
const openCount = computed(() => (props.unpaid.count > 0 ? 1 : 0) + (props.low.length > 0 ? 1 : 0));
</script>

<style scoped>
.inbox-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.inbox-heading {
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.inbox-open {
  padding: 2px 10px;
  border-radius: var(--radius-pill);
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
  font-size: var(--text-xs);
  font-weight: 600;
}

.inbox {
  margin: 0 -12px;
  padding: 0;
  list-style: none;
}

.inbox li + li {
  border-top: 1px solid var(--color-border-subtle);
}

/* Each item is one row you can act on: what it is, how much, and where it leads. */
.inbox-row {
  display: grid;
  grid-template-columns: 40px minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 14px;
  padding: 14px 12px;
  border-radius: var(--radius-md);
  color: inherit;
  text-decoration: none;
  transition: background var(--transition-fast);
}

a.inbox-row:hover {
  background: var(--color-surface-hover);
}

a.inbox-row:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: -2px;
}

.inbox-mark {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: var(--radius-md);
  font-size: 20px;
}

.tone-warning .inbox-mark {
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
}

.tone-danger .inbox-mark {
  background: var(--tone-danger-surface);
  color: var(--tone-danger-ink);
}

.settled .inbox-mark {
  background: var(--tint-success);
  color: var(--color-success-ink);
  font-size: 18px;
}

.inbox-main {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.inbox-title {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.settled .inbox-title {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.inbox-detail {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.inbox-figure {
  display: grid;
  justify-items: end;
  gap: 1px;
  text-align: right;
}

.inbox-figure strong {
  color: var(--color-text-primary);
  font-size: 18px;
  font-weight: 650;
  letter-spacing: -0.01em;
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.inbox-figure span {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.inbox-chevron {
  color: var(--color-text-faint);
  font-size: 18px;
  transition: transform var(--transition-fast), color var(--transition-fast);
}

a.inbox-row:hover .inbox-chevron {
  color: var(--color-primary);
  transform: translateX(2px);
}

.slot-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.slot-chip {
  display: inline-flex;
  align-items: baseline;
  gap: 6px;
  max-width: 100%;
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: var(--tone-warning-surface);
  font-size: var(--text-xs);
}

.slot-chip.critical {
  background: var(--tone-danger-surface);
}

.slot-chip-name {
  overflow: hidden;
  color: var(--color-text-primary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.slot-chip-count {
  flex: none;
  color: var(--tone-warning-ink);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.slot-chip.critical .slot-chip-count {
  color: var(--tone-danger-ink);
}

.slot-more {
  align-self: center;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

/* Nothing to do: the empty state is a result, so it reads as one. */
.all-clear {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 6px 0 2px;
}

.all-clear-mark {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--tint-success);
  color: var(--color-success-ink);
  font-size: 20px;
}

.all-clear-title {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.all-clear-text {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

@media (max-width: 520px) {
  .inbox-row {
    grid-template-columns: 40px minmax(0, 1fr) auto;
  }

  .inbox-figure {
    grid-column: 2;
    grid-row: 2;
    justify-items: start;
    text-align: left;
  }

  .inbox-chevron {
    grid-column: 3;
    grid-row: 1;
  }
}
</style>
