<template>
  <Drawer :open="open && expense !== null" :title="expense ? `Expense ${expense.id}` : 'Expense'" @close="emit('close')">
    <div v-if="expense" class="record-detail">
      <header class="detail-head">
        <p class="detail-name">{{ expense.category }}</p>
        <p v-if="expense.relatedPackage" class="detail-sub">{{ expense.relatedPackage }}</p>
        <p class="detail-figure">
          <strong :class="{ struck: expense.voided }">{{ peso(expense.amount) }}</strong>
          <span>{{ formatDate(expense.date) }}</span>
        </p>
        <dl class="detail-states">
          <div>
            <dt>Payment</dt>
            <dd><span class="record-badge" :class="badge(expense.paymentStatus)">{{ stateLabel(expense.paymentStatus) }}</span></dd>
          </div>
        </dl>
      </header>

      <div v-if="expense.voided" class="detail-voided" role="note">
        <UiIcon name="void" class="detail-voided-icon" />
        <div>
          <p class="detail-voided-title">Voided</p>
          <p v-if="expense.voidReason">{{ expense.voidReason }}</p>
          <p>Left out of every cost total. Restore it to count it again or to change it.</p>
        </div>
      </div>

      <div v-else-if="canMarkPaid" class="detail-quick" role="group" aria-label="Quick updates">
        <button class="secondary-button compact-button" type="button" :disabled="busy !== ''" @click="emit('mark-paid')">
          <UiIcon name="wallet" />
          {{ busy === 'paid' ? 'Saving…' : 'Mark as paid' }}
        </button>
      </div>

      <p v-if="actionError" class="drawer-form-error" role="alert">{{ actionError }}</p>

      <VoidPanel
        v-if="askVoid && !expense.voided"
        noun="expense"
        lead="Voiding keeps the expense and its history, but takes it out of every cost total."
        placeholder="Duplicate entry, refunded, recorded in error"
        :busy="busy === 'void'"
        :error="voidError"
        @cancel="askVoid = false"
        @confirm="(reason) => emit('void', reason)"
      />

      <dl class="detail-list">
        <div>
          <dt>Expense ID</dt>
          <dd>{{ expense.id }}</dd>
        </div>
        <div>
          <dt>Date</dt>
          <dd>{{ formatDate(expense.date) }}</dd>
        </div>
        <div>
          <dt>Category</dt>
          <dd>{{ expense.category }}</dd>
        </div>
        <div>
          <dt>Related to</dt>
          <dd>{{ expense.relatedPackage || 'Not linked to a package' }}</dd>
        </div>
        <div v-if="expense.notes">
          <dt>Notes</dt>
          <dd>{{ expense.notes }}</dd>
        </div>
      </dl>

      <RecordHistory v-if="canSeeHistory" entity-type="Expense" :code="expense.id" :version="historyVersion" />
    </div>

    <template v-if="expense" #footer>
      <template v-if="expense.voided">
        <span class="footer-spacer"></span>
        <button class="primary-button" type="button" :disabled="busy !== ''" @click="emit('restore')">
          <UiIcon name="restore" />
          {{ busy === 'restore' ? 'Restoring…' : 'Restore expense' }}
        </button>
      </template>
      <template v-else>
        <button class="danger-outline-button" type="button" :disabled="busy !== '' || askVoid" @click="askVoid = true">
          <UiIcon name="void" />
          Void expense
        </button>
        <span class="footer-spacer"></span>
        <button class="primary-button" type="button" :disabled="busy !== ''" aria-keyshortcuts="E" @click="emit('edit')">
          <UiIcon name="pencil" />
          Edit expense
        </button>
      </template>
    </template>
  </Drawer>
</template>

<script lang="ts">
export type ExpenseBusy = '' | 'paid' | 'void' | 'restore';
</script>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { ExpenseRow } from '../api';
import { peso, stateLabel } from '../format';
import { formatDate } from '../periods';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';
import VoidPanel from './VoidPanel.vue';

const props = withDefaults(
  defineProps<{
    open: boolean;
    expense: ExpenseRow | null;
    canSeeHistory: boolean;
    historyVersion?: number;
    busy?: ExpenseBusy;
    voidError?: string;
    actionError?: string;
  }>(),
  { historyVersion: 0, busy: '', voidError: '', actionError: '' },
);

const emit = defineEmits<{
  close: [];
  edit: [];
  'mark-paid': [];
  restore: [];
  void: [string];
}>();

const askVoid = ref(false);

const canMarkPaid = computed(() => props.expense !== null && props.expense.paymentStatus !== 'Paid');

// A different expense, or the panel reopening, starts without a half-finished void.
watch(
  () => [props.open, props.expense?.id, props.expense?.voided],
  () => {
    askVoid.value = false;
  },
);

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}
</script>
