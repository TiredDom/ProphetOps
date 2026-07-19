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
        <div class="stat-cell" :class="{ warn: pendingCount > 0 }">
          <span class="stat-label">Pending payments</span>
          <strong class="stat-value">{{ pendingCount }}</strong>
          <span class="stat-note">{{ pendingCount === 0 ? 'All settled' : peso(pendingAmount) + ' still owed' }}</span>
        </div>
      </section>

      <Drawer :open="showForm" :title="drawerTitle" @close="showForm = false">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <div class="form-grid">
          <label v-if="editing" class="account-field field-wide">
            <span>Expense number</span>
            <input :value="form.id" type="text" readonly />
          </label>
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
            <MoneyField v-model="form.amount" />
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
          <label class="account-field field-wide">
            <span>Notes</span>
            <input v-model.trim="form.notes" maxlength="240" />
          </label>
        </div>
        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : 'Save expense' }}
          </button>
        </template>
      </Drawer>

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="6" />
      </div>

      <EmptyState
        v-else-if="expenses.length === 0"
        title="No expenses yet"
        message="Record your first operating cost."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openForm">Add expense</button>
        </template>
      </EmptyState>

      <div v-else class="dss-grid two-column expense-layout">
        <section class="content-panel expense-ledger">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Expense ledger</h2>
              <span class="panel-meta">{{ filtered.length }} of {{ expenses.length }} shown</span>
            </div>
          </div>

          <div class="expense-toolbar">
            <SearchField v-model="query" placeholder="Search category or package" />
            <div class="chip-row" role="group" aria-label="Filter by category">
              <button
                v-for="chip in categoryChips"
                :key="chip.value"
                type="button"
                class="chip"
                :class="{ active: activeCategory === chip.value }"
                @click="activeCategory = chip.value"
              >
                {{ chip.label }}
                <span class="chip-count">{{ chip.count }}</span>
              </button>
            </div>
          </div>

          <div v-if="filtered.length === 0" class="no-match">
            <p>No expenses match your search.</p>
            <button class="table-link" type="button" @click="clearFilters">Clear filters</button>
          </div>

          <div v-else class="dss-table-frame">
            <table class="dss-table">
              <thead>
                <tr>
                  <th>Expense ID</th>
                  <th>Date</th>
                  <th>Category</th>
                  <th>Related package</th>
                  <th class="num">Amount</th>
                  <th>Payment status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="e in filtered" :key="e.id">
                  <td><strong>{{ e.id }}</strong></td>
                  <td class="cell-nowrap">{{ e.date }}</td>
                  <td>{{ e.category }}</td>
                  <td>{{ blank(e.relatedPackage) }}</td>
                  <td class="num"><strong>{{ peso(e.amount) }}</strong></td>
                  <td><span class="record-badge" :class="badge(e.paymentStatus)">{{ e.paymentStatus }}</span></td>
                  <td><button class="table-link" type="button" @click="openEdit(e)">Edit</button></td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <aside class="content-panel breakdown-panel">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Spending by category</h2>
            </div>
            <span class="panel-meta">{{ peso(totalAmount) }}</span>
          </div>

          <div class="breakdown">
            <button
              v-for="row in breakdown"
              :key="row.category"
              type="button"
              class="breakdown-row"
              :class="{ active: activeCategory === row.category }"
              @click="toggleCategory(row.category)"
            >
              <span class="breakdown-head">
                <span class="breakdown-name">{{ row.category }}</span>
                <strong class="breakdown-value">{{ peso(row.total) }}</strong>
              </span>
              <span class="breakdown-track">
                <span class="breakdown-fill" :style="{ width: barWidth(row.total) }"></span>
              </span>
              <span class="breakdown-foot">
                <span>{{ row.count }} {{ row.count === 1 ? 'expense' : 'expenses' }}</span>
                <span>{{ share(row.total) }}%</span>
              </span>
            </button>
          </div>
        </aside>
      </div>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import { useToast } from '../composables/useToast';
import { api, ApiError, type ExpenseRow, type ExpenseInput } from '../api';
import { peso } from '../format';
import MoneyField from '../components/MoneyField.vue';

const toast = useToast();

const expenses = ref<ExpenseRow[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');

const query = ref('');
const activeCategory = ref('All');

const CATEGORIES = ['Tour operations', 'Marketing', 'Seasonal cost', 'Overhead'];

const form = reactive<ExpenseInput>(emptyForm());

const drawerTitle = computed(() => (editing.value ? 'Edit expense' : 'New expense'));
const totalAmount = computed(() => expenses.value.reduce((sum, e) => sum + e.amount, 0));
const pendingCount = computed(() => expenses.value.filter((e) => e.paymentStatus === 'Pending').length);
const pendingAmount = computed(() =>
  expenses.value.filter((e) => e.paymentStatus === 'Pending').reduce((sum, e) => sum + e.amount, 0),
);

const filtered = computed(() => {
  const term = query.value.trim().toLowerCase();
  return expenses.value.filter((e) => {
    if (activeCategory.value !== 'All' && e.category !== activeCategory.value) return false;
    if (!term) return true;
    return (
      e.category.toLowerCase().includes(term) ||
      (e.relatedPackage ?? '').toLowerCase().includes(term) ||
      e.id.toLowerCase().includes(term)
    );
  });
});

const categoryChips = computed(() => [
  { value: 'All', label: 'All', count: expenses.value.length },
  ...CATEGORIES.map((c) => ({
    value: c,
    label: c,
    count: expenses.value.filter((e) => e.category === c).length,
  })).filter((c) => c.count > 0),
]);

const breakdown = computed(() => {
  const totals = new Map<string, { total: number; count: number }>();
  for (const e of expenses.value) {
    const entry = totals.get(e.category) ?? { total: 0, count: 0 };
    entry.total += e.amount;
    entry.count += 1;
    totals.set(e.category, entry);
  }
  return Array.from(totals.entries())
    .map(([category, v]) => ({ category, total: v.total, count: v.count }))
    .sort((a, b) => b.total - a.total);
});

const maxCategoryTotal = computed(() => breakdown.value.reduce((max, r) => Math.max(max, r.total), 0));

function barWidth(total: number): string {
  const max = maxCategoryTotal.value;
  if (max <= 0) return '2px';
  return Math.max(2, Math.round((total / max) * 100)) + '%';
}

function share(total: number): number {
  if (totalAmount.value <= 0) return 0;
  return Math.round((total / totalAmount.value) * 100);
}


function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function blank(value: string | null | undefined): string {
  return value && value.trim() ? value : '—';
}

function toggleCategory(category: string) {
  activeCategory.value = activeCategory.value === category ? 'All' : category;
}

function clearFilters() {
  query.value = '';
  activeCategory.value = 'All';
}

function emptyForm(): ExpenseInput {
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
  Object.assign(form, emptyForm());
  form.id = 'EXP-' + (3100 + expenses.value.length + 1);
  editing.value = false;
  originalCode.value = '';
  formError.value = '';
  showForm.value = true;
}

function openEdit(expense: ExpenseRow) {
  Object.assign(form, {
    id: expense.id,
    date: expense.date,
    category: expense.category,
    amount: expense.amount,
    relatedPackage: expense.relatedPackage,
    paymentStatus: expense.paymentStatus,
    notes: expense.notes ?? '',
  });
  editing.value = true;
  originalCode.value = expense.id;
  formError.value = '';
  showForm.value = true;
}

function validate(): string {
  if (!form.date) return 'Please choose the date of this expense.';
  if (!form.category) return 'Please choose a category.';
  if (typeof form.amount !== 'number' || Number.isNaN(form.amount) || form.amount < 0) {
    return 'Please enter an amount of zero or more.';
  }
  return '';
}

async function load() {
  loading.value = true;
  try {
    expenses.value = await api.expenses();
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (saving.value) return;
  const problem = validate();
  if (problem) {
    formError.value = problem;
    return;
  }
  formError.value = '';
  saving.value = true;
  const savedId = form.id;
  try {
    if (editing.value) {
      await api.updateExpense(originalCode.value, { ...form });
    } else {
      await api.createExpense({ ...form });
    }
    await load();
    showForm.value = false;
    toast.success('Expense ' + savedId + ' saved');
  } catch (e) {
    const message = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the expense.';
    formError.value = message;
    toast.error(message);
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

.expense-layout {
  align-items: start;
}

.expense-ledger {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.expense-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
  justify-content: space-between;
}

.chip-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 12.5px;
  font-weight: 500;
  padding: 5px 12px;
  cursor: pointer;
  transition: border-color var(--transition-fast), background var(--transition-fast), color var(--transition-fast);
}

.chip:hover {
  border-color: var(--color-text-faint);
  color: var(--color-text-primary);
}

.chip.active {
  border-color: var(--color-primary);
  background: var(--color-primary-soft);
  color: var(--color-primary-active);
  font-weight: 600;
}

.chip-count {
  color: var(--color-text-muted);
  font-size: 11.5px;
  font-variant-numeric: lining-nums tabular-nums;
}

.chip.active .chip-count {
  color: var(--color-primary);
}

.dss-table-frame {
  overflow-x: auto;
  min-width: 0;
}

.no-match {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-6) var(--space-2);
  color: var(--color-text-muted);
  font-size: 13.5px;
}

.breakdown-panel {
  position: sticky;
  top: var(--space-4);
}

.breakdown {
  display: flex;
  flex-direction: column;
}

.breakdown-row {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: 100%;
  text-align: left;
  border: none;
  background: none;
  cursor: pointer;
  padding: var(--space-3) var(--space-2);
  border-bottom: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-sm);
  transition: background var(--transition-fast);
}

.breakdown-row:last-child {
  border-bottom: none;
}

.breakdown-row:hover {
  background: var(--color-surface-sunken);
}

.breakdown-row.active {
  background: var(--color-primary-soft);
}

.breakdown-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
}

.breakdown-name {
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 500;
  min-width: 0;
}

.breakdown-value {
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.breakdown-track {
  position: relative;
  display: block;
  height: 8px;
  background: var(--color-surface-sunken);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.breakdown-fill {
  position: absolute;
  inset: 0 auto 0 0;
  min-width: 2px;
  background: var(--color-primary);
  border-radius: var(--radius-pill);
}

.breakdown-row.active .breakdown-fill {
  background: var(--color-accent);
}

.breakdown-foot {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  color: var(--color-text-muted);
  font-size: 12px;
  font-variant-numeric: lining-nums tabular-nums;
}

@media (max-width: 1024px) {
  .breakdown-panel {
    position: static;
  }
}

@media (max-width: 640px) {
  .expense-toolbar {
    align-items: stretch;
  }

  .chip-row {
    width: 100%;
  }
}
</style>
