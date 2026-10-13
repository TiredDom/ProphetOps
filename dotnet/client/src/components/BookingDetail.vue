<template>
  <Drawer :open="open && booking !== null" :title="booking ? `Booking ${booking.id}` : 'Booking'" @close="emit('close')">
    <div v-if="booking" class="record-detail">
      <header class="detail-head">
        <p class="detail-name">{{ booking.client }}</p>
        <p class="detail-sub">
          {{ booking.package }}<template v-if="booking.destination"> · {{ booking.destination }}</template>
        </p>
        <p class="detail-figure">
          <strong :class="{ struck: booking.voided }">{{ peso(booking.grossRevenue) }}</strong>
          <span>{{ booking.y }} {{ booking.y === 1 ? 'passenger' : 'passengers' }}</span>
        </p>
        <dl class="detail-states">
          <div>
            <dt>Payment</dt>
            <dd><span class="record-badge" :class="badge(booking.paymentStatus)">{{ booking.paymentStatus }}</span></dd>
          </div>
          <div>
            <dt>Booking</dt>
            <dd><span class="record-badge" :class="badge(booking.bookingStatus)">{{ booking.bookingStatus }}</span></dd>
          </div>
        </dl>
      </header>

      <div v-if="booking.voided" class="detail-voided" role="note">
        <UiIcon name="void" class="detail-voided-icon" />
        <div>
          <p class="detail-voided-title">Voided</p>
          <p v-if="booking.voidReason">{{ booking.voidReason }}</p>
          <p>Left out of every total and the forecast. Restoring it counts it again and takes its seats back.</p>
        </div>
      </div>

      <div v-else-if="canMarkPaid || canConfirm" class="detail-quick" role="group" aria-label="Quick updates">
        <button
          v-if="canMarkPaid"
          class="secondary-button compact-button"
          type="button"
          :disabled="busy !== ''"
          @click="emit('mark-paid')"
        >
          <UiIcon name="wallet" />
          {{ busy === 'paid' ? 'Saving…' : 'Mark as paid' }}
        </button>
        <button
          v-if="canConfirm"
          class="secondary-button compact-button"
          type="button"
          :disabled="busy !== ''"
          @click="emit('confirm')"
        >
          <UiIcon name="check" />
          {{ busy === 'confirm' ? 'Saving…' : 'Confirm booking' }}
        </button>
      </div>

      <p v-if="actionError" class="drawer-form-error" role="alert">{{ actionError }}</p>

      <div v-if="askVoid && !booking.voided" ref="voidPanel" class="void-panel" role="group" aria-label="Void this booking">
        <p class="void-lead">
          Voiding keeps the record and its history, but takes it out of every total and out of the forecast.
          Any seats it holds go back to the package.
        </p>
        <label class="account-field">
          <span>Why is it being voided?</span>
          <input
            ref="reasonInput"
            v-model.trim="reason"
            maxlength="160"
            placeholder="Duplicate entry, client cancelled, recorded in error"
          />
        </label>
        <p v-if="voidError" class="drawer-form-error" role="alert">{{ voidError }}</p>
        <div class="void-actions">
          <button class="secondary-button" type="button" :disabled="busy === 'void'" @click="askVoid = false">Keep it</button>
          <button class="danger-button" type="button" :disabled="busy === 'void' || !reason" @click="emit('void', reason)">
            {{ busy === 'void' ? 'Voiding…' : 'Void booking' }}
          </button>
        </div>
      </div>

      <dl class="detail-list">
        <div>
          <dt>Booking date</dt>
          <dd>{{ formatDate(booking.ds) }}</dd>
        </div>
        <div>
          <dt>Trip</dt>
          <dd>{{ booking.packageId == null ? 'Tailored (custom) trip' : `Ready-made package ${booking.packageId}` }}</dd>
        </div>
        <div>
          <dt>Staff</dt>
          <dd>{{ booking.staffAssigned || 'Not recorded' }}</dd>
        </div>
        <div>
          <dt>Recorded from</dt>
          <dd>{{ booking.source }}</dd>
        </div>
        <div v-if="booking.notes">
          <dt>Notes</dt>
          <dd>{{ booking.notes }}</dd>
        </div>
      </dl>

      <RecordHistory v-if="canSeeHistory" entity-type="Booking" :code="booking.id" :version="historyVersion" />
    </div>

    <template v-if="booking" #footer>
      <template v-if="booking.voided">
        <span class="footer-spacer"></span>
        <button class="primary-button" type="button" :disabled="busy !== ''" @click="emit('restore')">
          <UiIcon name="restore" />
          {{ busy === 'restore' ? 'Restoring…' : 'Restore booking' }}
        </button>
      </template>
      <template v-else>
        <button class="table-link danger-link" type="button" :disabled="busy !== '' || askVoid" @click="startVoid">
          Void booking
        </button>
        <span class="footer-spacer"></span>
        <button class="primary-button" type="button" :disabled="busy !== ''" @click="emit('edit')">
          <UiIcon name="pencil" />
          Edit booking
        </button>
      </template>
    </template>
  </Drawer>
</template>

<script lang="ts">
export type BookingBusy = '' | 'paid' | 'confirm' | 'void' | 'restore';
</script>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import type { Booking } from '../api';
import { peso } from '../format';
import { formatDate } from '../periods';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';

const props = withDefaults(
  defineProps<{
    open: boolean;
    booking: Booking | null;
    canSeeHistory: boolean;
    historyVersion?: number;
    busy?: BookingBusy;
    voidError?: string;
    actionError?: string;
  }>(),
  { historyVersion: 0, busy: '', voidError: '', actionError: '' },
);

const emit = defineEmits<{
  close: [];
  edit: [];
  'mark-paid': [];
  confirm: [];
  restore: [];
  void: [string];
}>();

const askVoid = ref(false);
const reason = ref('');
const voidPanel = ref<HTMLElement | null>(null);
const reasonInput = ref<HTMLInputElement | null>(null);

const canMarkPaid = computed(() => props.booking !== null && props.booking.paymentStatus !== 'Paid');
const canConfirm = computed(() => props.booking !== null && props.booking.bookingStatus !== 'Confirmed');

// A different booking, or the panel reopening, starts without a half-finished void.
watch(
  () => [props.open, props.booking?.id],
  () => {
    askVoid.value = false;
    reason.value = '';
  },
);

async function startVoid() {
  askVoid.value = true;
  await nextTick();
  voidPanel.value?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  reasonInput.value?.focus();
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}
</script>

<style scoped>
.record-detail {
  display: grid;
  gap: 20px;
}

.detail-head {
  display: grid;
  gap: 4px;
}

.detail-name {
  color: var(--color-text-primary);
  font-size: var(--text-lg);
  font-weight: 650;
  letter-spacing: -0.01em;
  line-height: 1.25;
  overflow-wrap: anywhere;
}

.detail-sub {
  color: var(--color-text-secondary);
  font-size: var(--text-base);
}

.detail-figure {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 12px;
  margin-top: 10px;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.detail-figure strong {
  color: var(--color-text-primary);
  font-size: var(--text-xl);
  font-weight: 650;
  letter-spacing: -0.015em;
  font-variant-numeric: lining-nums tabular-nums;
}

.detail-figure strong.struck {
  color: var(--color-text-muted);
  text-decoration: line-through;
  text-decoration-color: var(--color-text-faint);
}

.detail-states {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 24px;
  margin: 6px 0 0;
}

.detail-states > div {
  display: flex;
  align-items: center;
  gap: 8px;
  line-height: 20px;
}

.detail-states dt {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 20px;
}

.detail-states dd {
  margin: 0;
}

.detail-states .record-badge {
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  line-height: 20px;
  vertical-align: top;
}

.detail-quick {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: -6px;
}

.detail-quick .ui-icon {
  font-size: 16px;
}

.detail-voided {
  display: flex;
  gap: 12px;
  padding: 12px 14px;
  border: 1px solid var(--tone-danger-border);
  border-radius: var(--radius-md);
  background: var(--tone-danger-surface);
  color: var(--tone-danger-ink);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.detail-voided-icon {
  margin-top: 2px;
  font-size: 18px;
}

.detail-voided-title {
  font-weight: 600;
}
</style>
