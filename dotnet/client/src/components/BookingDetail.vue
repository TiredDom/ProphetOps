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
            <dd><span class="record-badge" :class="badge(booking.paymentStatus)">{{ stateLabel(booking.paymentStatus) }}</span></dd>
          </div>
          <div>
            <dt>Booking</dt>
            <dd><span class="record-badge" :class="badge(booking.bookingStatus)">{{ stateLabel(booking.bookingStatus) }}</span></dd>
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

      <VoidPanel
        v-if="askVoid && !booking.voided"
        noun="booking"
        lead="Voiding keeps the record and its history, but takes it out of every total and out of the forecast. Any seats it holds go back to the package."
        placeholder="Duplicate entry, client cancelled, recorded in error"
        :busy="busy === 'void'"
        :error="voidError"
        @cancel="askVoid = false"
        @confirm="(reason) => emit('void', reason)"
      />

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
        <button class="danger-outline-button" type="button" :disabled="busy !== '' || askVoid" @click="startVoid">
          <UiIcon name="void" />
          Void booking
        </button>
        <span class="footer-spacer"></span>
        <button class="primary-button" type="button" :disabled="busy !== ''" aria-keyshortcuts="E" @click="emit('edit')">
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
import { computed, ref, watch } from 'vue';
import type { Booking } from '../api';
import { peso, stateLabel } from '../format';
import { formatDate } from '../periods';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';
import VoidPanel from './VoidPanel.vue';

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

const canMarkPaid = computed(() => props.booking !== null && props.booking.paymentStatus !== 'Paid');
const canConfirm = computed(() => props.booking !== null && props.booking.bookingStatus !== 'Confirmed');

// A different booking, or the panel reopening, starts without a half-finished void.
watch(
  () => [props.open, props.booking?.id],
  () => {
    askVoid.value = false;
  },
);

function startVoid() {
  askVoid.value = true;
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}
</script>
