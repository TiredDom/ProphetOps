<template>
  <AppShell title="Expenses" description="Recorded operating costs.">
    <template #actions>
      <button class="primary-button" type="button" @click="openForm">Add expense</button>
    </template>

    <section class="dss-page">
      <section class="stat-band" aria-label="Expense totals">
        <div class="stat-cell">
          <span class="stat-label">Expenses</span>
          <strong class="stat-value">{{ expenses.length }}</strong>
          <span class="stat-note">Saved expenses</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Total costs</span>
          <strong class="stat-value">{{ peso(totalAmount) }}</strong>
          <span class="stat-note">Recorded to date</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Pending payments</span>
          <strong class="stat-value">{{ pendingCount }}</strong>
          <span class="stat-note">Awaiting payment</span>
        </div>
      </section>

      <Drawer :open="showForm" title="New expense" @close="showForm = false">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <div class="form-grid">
          <label class="account-field">
            <span>Date</span>
            <input v-model="form.date" type="date" />
          </label>
          <label class="account-field">
            <span>Category</span>
            <select v-model="form.category">
              <option>Tour operations</option>
              <option>Marketing</option>
              <option>Seasonal cost</option>
              <option>Overhead</option>
            </select>
          </label>
          <label class="account-field">
            <span>Amount</span>
            <input v-model.number="form.amount" type="number" min="0" />
          </label>
          <label class="account-field">
            <span>Payment status</span>
            <select v-model="form.paymentStatus">
              <option>Paid</option>
              <option>Pending</option>
            </select>
          </label>
          <label class="account-field field-wide">
            <span>Related package or operation</span>
            <input v-model.trim="form.relatedPackage" maxlength="120" />
          </label>
        </div>
        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : 'Save expense' }}
          </button>
        </template>
      </Drawer>

      <div class="dss-table-frame">
        <table class="dss-table">
          <thead>
            <tr>
              <th>Expense ID</th>
              <th>Date</th>
              <th>Category</th>
              <th>Related package</th>
              <th class="num">Amount</th>
              <th>Payment status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="e in expenses" :key="e.id">
              <td><strong>{{ e.id }}</strong></td>
              <td>{{ e.date }}</td>
              <td>{{ e.category }}</td>
              <td>{{ e.relatedPackage }}</td>
              <td class="num"><strong>{{ peso(e.amount) }}</strong></td>
              <td><span class="record-badge" :class="badge(e.paymentStatus)">{{ e.paymentStatus }}</span></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import { api, ApiError, type ExpenseRow, type ExpenseInput } from '../api';

const expenses = ref<ExpenseRow[]>([]);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');

const form = reactive<ExpenseInput>(blank());

const totalAmount = computed(() => expenses.value.reduce((sum, e) => sum + e.amount, 0));
const pendingCount = computed(() => expenses.value.filter((e) => e.paymentStatus === 'Pending').length);

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function blank(): ExpenseInput {
  return {
    id: '',
    date: new Date().toISOString().slice(0, 10),
    category: 'Tour operations',
    amount: 0,
    relatedPackage: '',
    paymentStatus: 'Pending',
    notes: '',
  };
}

function openForm() {
  Object.assign(form, blank());
  form.id = 'EXP-' + (3100 + expenses.value.length + 1);
  formError.value = '';
  showForm.value = true;
}

async function load() {
  expenses.value = await api.expenses();
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  saving.value = true;
  try {
    await api.createExpense({ ...form });
    await load();
    showForm.value = false;
  } catch (e) {
    formError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the expense.';
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<style scoped>
.drawer-form-error {
  margin-bottom: var(--space-4);
  color: var(--color-danger-ink);
  font-size: 13px;
}
</style>
