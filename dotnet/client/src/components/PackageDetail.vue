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
import type { PackageRow } from '../api';
import { count, peso, stateLabel } from '../format';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';

const props = withDefaults(
  defineProps<{
    open: boolean;
    pkg: PackageRow | null;
    canSeeHistory: boolean;
    canBook: boolean;
    historyVersion?: number;
  }>(),
  { historyVersion: 0 },
);

const emit = defineEmits<{ close: []; edit: []; 'view-photo': [] }>();

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
