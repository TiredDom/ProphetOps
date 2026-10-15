<template>
  <Drawer :open="open && pkg !== null" :title="pkg ? `Package ${pkg.id}` : 'Package'" @close="emit('close')">
    <div v-if="pkg" class="record-detail">
      <header class="detail-head">
        <p class="detail-name">{{ pkg.packageName }}</p>
        <p class="detail-sub">{{ place }}</p>
        <p class="detail-figure">
          <strong>{{ peso(pkg.basePrice) }}</strong>
          <span>per passenger</span>
        </p>
        <dl class="detail-states">
          <div>
            <dt>Status</dt>
            <dd><span class="record-badge" :class="badge(pkg.status)">{{ stateLabel(pkg.status) }}</span></dd>
          </div>
          <div>
            <dt>Slots</dt>
            <dd><span class="slots-now" :class="tone">{{ slotsLeft }}</span></dd>
          </div>
        </dl>
      </header>

      <div class="package-slots">
        <span class="slot-meter" aria-hidden="true">
          <span class="slot-sold" :style="{ width: share(pkg.soldCount) }"></span>
          <span class="slot-reserved" :style="{ width: share(pkg.reservedCount) }"></span>
        </span>
        <p class="detail-note">
          {{ count(pkg.soldCount) }} sold · {{ count(pkg.reservedCount) }} reserved · {{ count(pkg.availableSlots) }} open,
          of {{ count(capacity) }} in all
        </p>
      </div>

      <div class="detail-links">
        <RouterLink
          v-if="canBook && pkg.availableSlots > 0"
          class="detail-link"
          :to="{ path: '/bookings', query: { new: '1', package: pkg.id } }"
        >
          <UiIcon name="plus" /> Book this package
        </RouterLink>
        <RouterLink class="detail-link" :to="{ path: '/bookings', query: { q: pkg.packageName } }">
          See its bookings <UiIcon name="arrow-right" />
        </RouterLink>
      </div>

      <section v-if="demand" class="demand" aria-labelledby="demand-title">
        <h3 id="demand-title" class="demand-heading">
          <UiIcon name="forecast" class="demand-mark" />
          Expected demand
        </h3>

        <p v-if="demand.status === 'no-history'" class="demand-text">
          No passengers were booked on this package<template v-if="basis"> from {{ basis.from }} to {{ basis.to }}</template>,
          so there is nothing to estimate its demand from.
        </p>

        <template v-else>
          <p class="demand-text">
            About <strong class="model-figure">{{ passengers(demand.expected.value) }}</strong> are expected
            {{ span }} ({{ whole(demand.expected.lower) }} to {{ whole(demand.expected.upper) }}), going by its
            {{ percent(demand.share) }} share of the passengers booked<template v-if="basis"> from {{ basis.from }} to {{ basis.to }}</template>.
          </p>
          <p class="demand-verdict" :class="demand.status">{{ verdict }}</p>

          <div v-if="demand.price" class="price-idea">
            <p class="price-line">
              {{ demand.price.kind === 'raise' ? 'Price to consider:' : 'A limited offer to consider:' }}
              <strong>{{ peso(demand.price.suggestedPrice) }}</strong> <span class="price-change">{{ signed(demand.price.changePercent) }}</span>
              instead of {{ peso(demand.price.currentPrice) }}.
            </p>
            <p class="price-note">Bookings already made keep their prices, and nothing changes until you save the package.</p>
            <button type="button" class="secondary-button price-use" @click="emit('use-price', demand.price.suggestedPrice)">
              Use this price
            </button>
          </div>

          <p class="demand-note">
            An estimate: packages have no travel dates, so this sets passengers booked by month against the slots open
            today.
          </p>
        </template>
      </section>

      <button
        v-if="pkg.imageUrl"
        type="button"
        class="package-photo"
        :aria-label="`View photo of ${pkg.packageName}`"
        @click="emit('view-photo')"
      >
        <img :src="pkg.imageUrl" alt="" />
      </button>

      <dl class="detail-list">
        <div>
          <dt>Package ID</dt>
          <dd>{{ pkg.id }}</dd>
        </div>
        <div>
          <dt>Destination</dt>
          <dd>{{ pkg.destination }}</dd>
        </div>
        <div>
          <dt>Duration</dt>
          <dd>{{ pkg.duration || 'Not set' }}</dd>
        </div>
        <div>
          <dt>Inclusions</dt>
          <dd>{{ pkg.inclusions || 'Not listed' }}</dd>
        </div>
      </dl>

      <RecordHistory v-if="canSeeHistory" entity-type="TravelPackage" :code="pkg.id" :version="historyVersion" />
    </div>

    <template v-if="pkg" #footer>
      <span class="footer-spacer"></span>
      <button class="primary-button" type="button" aria-keyshortcuts="E" @click="emit('edit')">
        <UiIcon name="pencil" />
        Edit package
      </button>
    </template>
  </Drawer>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PackageDemand, PackageRow } from '../api';
import { count, peso, stateLabel } from '../format';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';

/**
 * `demand` is this package's part of the passenger forecast, for Owner and Admin; `demandMonths` the
 * months it covers and `basis` the twelve months its share comes from.
 */
const props = withDefaults(
  defineProps<{
    open: boolean;
    pkg: PackageRow | null;
    canSeeHistory: boolean;
    canBook: boolean;
    historyVersion?: number;
    demand?: PackageDemand | null;
    demandMonths?: string[];
    basis?: { from: string; to: string } | null;
  }>(),
  { historyVersion: 0, demand: null, demandMonths: () => [], basis: null },
);

const emit = defineEmits<{ close: []; edit: []; 'view-photo': []; 'use-price': [price: number] }>();

const span = computed(() => {
  const months = props.demandMonths;
  if (months.length === 0) return 'over the coming months';
  if (months.length === 1) return `in ${months[0]}`;
  return `from ${months[0]} to ${months[months.length - 1]}`;
});

const verdict = computed(() => {
  const d = props.demand;
  if (!d) return '';
  if (d.openSlots === 0) return 'It is full, so these passengers would need slots added.';
  const slots = `${count(d.openSlots)} open ${d.openSlots === 1 ? 'slot' : 'slots'}`;
  if (d.status === 'likely-short') return `That is more than its ${slots} even at the low end: likely to run short.`;
  if (d.status === 'could-short') return `The high end is more than its ${slots}: it could run short.`;
  // The verdict carries the reason for a suggested price, so the price box need not repeat it.
  if (d.price?.kind === 'offer') return `Even the high end would fill no more than half of its ${slots}.`;
  return `Its ${slots} cover even the high end.`;
});

function whole(value: number): string {
  return count(Math.round(value));
}

function passengers(value: number): string {
  const n = Math.round(value);
  return `${count(n)} ${n === 1 ? 'passenger' : 'passengers'}`;
}

function percent(share: number): string {
  const value = Math.round(share * 1000) / 10;
  return `${value.toFixed(value < 10 ? 1 : 0).replace(/\.0$/, '')}%`;
}

function signed(change: number): string {
  return `${change > 0 ? '+' : '−'}${Math.abs(change).toFixed(change % 1 === 0 ? 0 : 1)}%`;
}

const capacity = computed(() => {
  const p = props.pkg;
  return p ? p.availableSlots + p.soldCount + p.reservedCount : 0;
});

const place = computed(() => {
  const p = props.pkg;
  if (!p) return '';
  return p.duration ? `${p.destination} · ${p.duration}` : p.destination;
});

const slotsLeft = computed(() => {
  const p = props.pkg;
  if (!p) return '';
  if (p.availableSlots === 0) return 'Full';
  return p.availableSlots === 1 ? '1 left' : `${count(p.availableSlots)} left`;
});

const tone = computed(() => {
  const p = props.pkg;
  if (!p) return '';
  if (p.status === 'Critical' || p.availableSlots === 0) return 'tone-danger';
  if (p.status === 'Low') return 'tone-warning';
  return '';
});

function share(part: number): string {
  return capacity.value > 0 ? `${Math.min(100, (part / capacity.value) * 100)}%` : '0%';
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}
</script>

<style scoped>
.package-slots {
  display: grid;
  gap: 8px;
  margin-top: -6px;
}

.package-slots .slot-meter {
  max-width: none;
  height: 8px;
}

.slots-now {
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.slots-now.tone-warning {
  color: var(--color-warning-ink);
}

.slots-now.tone-danger {
  color: var(--color-danger-ink);
}

.demand {
  display: grid;
  gap: 8px;
  padding: 14px 0 2px;
  border-top: 1px solid var(--color-border-subtle);
}

.demand-heading {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
}

.demand-mark {
  color: var(--color-model);
  font-size: 17px;
}

.demand-text,
.demand-verdict,
.price-line {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.5;
}

.demand-text strong.model-figure {
  color: var(--color-model-ink);
  font-weight: 600;
}

.demand-verdict {
  font-weight: 600;
}

.demand-verdict.likely-short {
  color: var(--color-danger-ink);
}

.demand-verdict.could-short {
  color: var(--color-warning-ink);
}

.demand-verdict.enough {
  color: var(--color-success-ink);
}

.price-idea {
  display: grid;
  gap: 6px;
  justify-items: start;
  margin-top: 4px;
  padding: 12px 14px;
  border: 1px solid var(--color-model-border);
  border-radius: var(--radius-md);
  background: var(--color-model-wash);
}

.price-line strong {
  color: var(--color-text-primary);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.price-change {
  color: var(--color-model-ink);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.price-note,
.demand-note {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.price-use {
  margin-top: 4px;
}

.package-photo {
  display: block;
  width: 100%;
  max-height: 220px;
  padding: 0;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface-sunken);
  cursor: zoom-in;
}

.package-photo img {
  display: block;
  width: 100%;
  height: 100%;
  max-height: 220px;
  object-fit: cover;
}
</style>
