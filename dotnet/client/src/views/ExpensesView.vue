<template>
  <AppShell title="Expenses">
    <template #actions>
      <a
        class="secondary-button"
        :href="exportHref"
        :title="range ? `Download the expenses for ${describeRange(range)} as a CSV file` : 'Download every expense as a CSV file'"
      >
        <UiIcon name="download" />
        Export CSV
      </a>
      <button class="primary-button" type="button" @click="openForm">
        <UiIcon name="plus" />
        Add expense
      </button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="drawerTitle" @close="showForm = false">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>

        <div v-if="editingExpense?.voided" class="voided-notice" role="note">
          <UiIcon name="void" class="voided-notice-icon" />
          <div>
            <p class="voided-notice-title">Voided</p>
            <p v-if="editingExpense.voidReason">{{ editingExpense.voidReason }}</p>
            <p>Left out of every total. Restore it to count it again or to change it.</p>
          </div>
        </div>

        <div v-if="askVoid && editing && !editingExpense?.voided" ref="voidPanel" class="void-panel" role="group" aria-label="Void this expense">
          <p class="void-lead">
            Voiding keeps the expense and its history, but takes it out of every cost total.
          </p>
          <label class="account-field">
            <span>Why is it being voided?</span>
            <input
              ref="reasonInput"
              v-model.trim="voidReason"
              maxlength="160"
              placeholder="Duplicate entry, refunded, recorded in error"
            />
          </label>
          <p v-if="voidError" class="drawer-form-error" role="alert">{{ voidError }}</p>
          <div class="void-actions">
            <button class="secondary-button" type="button" :disabled="voiding" @click="askVoid = false">Keep it</button>
            <button class="danger-button" type="button" :disabled="voiding || !voidReason" @click="confirmVoid">
              {{ voiding ? 'Voiding…' : 'Void expense' }}
            </button>
          </div>
        </div>

        <fieldset class="form-fieldset" :disabled="!!editingExpense?.voided">
          <legend class="visually-hidden">Expense details</legend>
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
                <option v-for="c in CATEGORIES" :key="c">{{ c }}</option>
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
        </fieldset>

        <RecordHistory
          v-if="editing && canSeeHistory"
          class="form-history"
          entity-type="Expense"
          :code="originalCode"
          :version="historyVersion"
        />

        <template #footer>
          <template v-if="editingExpense?.voided">
            <button class="secondary-button" type="button" :disabled="restoring" @click="showForm = false">Close</button>
            <button class="primary-button" type="button" :disabled="restoring" @click="restoreExpense">
              <UiIcon name="restore" />
              {{ restoring ? 'Restoring…' : 'Restore expense' }}
            </button>
          </template>
          <template v-else>
            <button
              v-if="editing"
              class="table-link danger-link"
              type="button"
              :disabled="saving || askVoid"
              @click="startVoid"
            >Void expense</button>
            <span class="footer-spacer"></span>
            <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
            <button class="primary-button" type="button" :disabled="saving" @click="save">
              {{ saving ? 'Saving…' : editing ? 'Save changes' : 'Save expense' }}
            </button>
          </template>
        </template>
      </Drawer>

      <SpendingGuide
        v-if="!loading && expenses.length"
        :active-from="periodKey === 'custom' ? filters.from : ''"
        @show-month="showMonth"
      />

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="8" />
      </div>

      <div v-else-if="loadError" class="content-panel load-error" role="alert">
        <p class="load-error-title">The expenses could not be loaded</p>
        <p class="load-error-text">Check the connection, then try again. Nothing has been changed.</p>
        <button class="secondary-button" type="button" @click="retryLoad">Try again</button>
      </div>
      <EmptyState
        v-else-if="expenses.length === 0"
        title="No expenses yet"
        message="Record your first operating cost to see where the money goes."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openForm">Add expense</button>
        </template>
      </EmptyState>

      <section v-else class="content-panel records-panel" aria-label="Expenses">
        <div class="records-tools">
          <SearchField v-model="filters.q" placeholder="Search category, package, notes or ID" />
          <PeriodSelect
            v-model:period="periodKey"
            v-model:from="filters.from"
            v-model:to="filters.to"
            :today="businessToday"
          />
          <button
            type="button"
            class="filter-chip"
            :aria-pressed="pendingOnly"
            @click="filters.pending = pendingOnly ? '' : '1'"
          >
            Pending
            <span class="chip-count">{{ count(pendingInScope) }}</span>
          </button>
          <button
            v-if="voidedInScope > 0"
            type="button"
            class="filter-chip"
            :aria-pressed="showVoided"
            @click="filters.voided = showVoided ? '' : '1'"
          >
            Show voided
            <span class="chip-count">{{ count(voidedInScope) }}</span>
          </button>
          <label v-if="stacked" class="records-sort">
            <span class="visually-hidden">Sort expenses</span>
            <select v-model="filters.sort">
              <option v-for="option in sortOptions" :key="option.key" :value="option.key">{{ option.label }}</option>
            </select>
          </label>
        </div>

        <div v-if="categoryTotals.length" class="category-split">
          <div class="category-bar" role="img" :aria-label="categoryDescription">
            <span
              v-for="c in categoryTotals"
              :key="c.category"
              class="category-bar-part"
              :class="{ dim: filters.category !== '' && filters.category !== c.category }"
              :style="{ flexGrow: c.total, background: c.color }"
            ></span>
          </div>
          <div class="category-chips" role="group" aria-label="Filter by category">
            <button
              type="button"
              class="filter-chip"
              :aria-pressed="filters.category === ''"
              @click="filters.category = ''"
            >
              All categories
            </button>
            <button
              v-for="c in categoryTotals"
              :key="c.category"
              type="button"
              class="filter-chip category-chip"
              :aria-pressed="filters.category === c.category"
              @click="filters.category = filters.category === c.category ? '' : c.category"
            >
              <span class="category-swatch" :style="{ background: c.color }" aria-hidden="true"></span>
              {{ c.category }}
              <span class="category-amount">{{ pesoCompact(c.total) }}</span>
              <span class="chip-count">{{ c.share }}%</span>
            </button>
          </div>
        </div>

        <p class="records-summary" aria-live="polite">
          <span class="summary-figure"><strong>{{ count(active.length) }}</strong> {{ active.length === 1 ? 'expense' : 'expenses' }}</span>
          <span class="summary-figure"><strong>{{ peso(activeTotal) }}</strong> total</span>
          <span v-if="activePending.length" class="summary-figure tone-warning">
            <strong>{{ peso(activePendingTotal) }}</strong> still owed on {{ activePending.length }} pending
          </span>
          <span class="summary-scope">{{ scopeText }}</span>
          <button v-if="isFiltered" class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </p>

        <template v-if="matching.length">
          <ul v-if="stacked" class="record-list">
            <template v-for="section in sections" :key="section.key">
              <li v-if="section.label && section.summary" class="record-list-month">
                <span>{{ section.label }}</span>
                <span>{{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'expense' : 'expenses' }} · {{ peso(section.summary.total) }}</span>
              </li>
              <li v-for="e in section.items" :key="e.id">
                <button class="record-item" :class="{ 'is-voided': e.voided }" type="button" @click="openEdit(e)">
                  <span class="record-item-top">
                    <span class="record-item-title">{{ e.category }}</span>
                    <span class="record-item-figure">{{ peso(e.amount) }}</span>
                  </span>
                  <span v-if="e.relatedPackage || e.notes" class="record-item-sub">{{ e.relatedPackage || e.notes }}</span>
                  <span class="record-item-meta">
                    <span>{{ dateLabel(e.date) }} · {{ e.id }}<template v-if="e.voided"> · Voided</template></span>
                    <span class="record-badge" :class="badge(e.paymentStatus)">{{ stateLabel(e.paymentStatus) }}</span>
                  </span>
                </button>
              </li>
            </template>
          </ul>

          <div v-else class="dss-table-frame">
            <table class="dss-table records-table expenses-table">
              <thead>
                <tr>
                  <SortHeader label="Date" field="date" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Expense</th>
                  <th scope="col">Related to</th>
                  <th scope="col">Notes</th>
                  <SortHeader label="Amount" field="amount" align="right" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Payment</th>
                </tr>
              </thead>
              <tbody v-for="section in sections" :key="section.key">
                <tr v-if="section.label && section.summary" class="month-row">
                  <th scope="rowgroup" colspan="4">
                    {{ section.label }}
                    <span class="month-count">· {{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'expense' : 'expenses' }}</span>
                  </th>
                  <td class="num">{{ peso(section.summary.total) }}</td>
                  <td>
                    <span v-if="section.summary.pending" class="month-count">{{ section.summary.pending }} pending</span>
                  </td>
                </tr>
                <tr
                  v-for="e in section.items"
                  :key="e.id"
                  class="record-row"
                  :class="{ 'is-voided': e.voided }"
                  @click="openEdit(e)"
                >
                  <td class="cell-date">{{ dateLabel(e.date) }}</td>
                  <td>
                    <button class="row-open" type="button" @click.stop="openEdit(e)">{{ e.category }}</button>
                    <span class="row-subtext">
                      {{ e.id }}<span v-if="e.voided" class="voided-tag" :title="e.voidReason ?? ''">Voided</span>
                    </span>
                  </td>
                  <td class="related-cell">{{ e.relatedPackage || '—' }}</td>
                  <td class="notes-cell" :title="e.notes ?? ''">{{ e.notes || '—' }}</td>
                  <td class="num"><strong>{{ peso(e.amount) }}</strong></td>
                  <td><span class="record-badge" :class="badge(e.paymentStatus)">{{ stateLabel(e.paymentStatus) }}</span></td>
                </tr>
              </tbody>
            </table>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            :page-size="PAGE"
            noun="expenses"
            @more="loadMore"
            @all="showAll"
          />
        </template>

        <div v-else class="records-empty">
          <strong>No expenses match</strong>
          <span>Try a different search, category or period.</span>
          <button class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </div>
      </section>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import ListFooter from '../components/ListFooter.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import RecordHistory from '../components/RecordHistory.vue';
import SortHeader from '../components/SortHeader.vue';
import UiIcon from '../components/UiIcon.vue';
import MoneyField from '../components/MoneyField.vue';
import SpendingGuide from '../components/forecast/SpendingGuide.vue';
import { usePaged } from '../composables/usePaged';
import { useToast } from '../composables/useToast';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useQueryState } from '../composables/useQueryState';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { api, ApiError, type ExpenseRow, type ExpenseInput } from '../api';
import { count, peso, pesoCompact, stateLabel } from '../format';
import { describeRange, formatDate, formatDay, inRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';
import { monthSections, nextSort, singleSection, sortParts, type SortDirection } from '../listing';

const PAGE = 50;
const CATEGORIES = ['Tour operations', 'Marketing', 'Seasonal cost', 'Overhead'];
// One navy family, darkest for the first category, so the split reads as shares of one whole.
const CATEGORY_COLORS = ['#16346B', '#3D68A8', '#7A9DD0', '#B3C6E4'];
const OTHER_COLOR = '#8A96AC';

const toast = useToast();
const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();

const expenses = ref<ExpenseRow[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');
const historyVersion = ref(0);
const canSeeHistory = computed(() => canAccess(auth.user?.role, '/reports'));

const askVoid = ref(false);
const voidReason = ref('');
const voidError = ref('');
const voiding = ref(false);
const restoring = ref(false);
const voidPanel = ref<HTMLElement | null>(null);
const reasonInput = ref<HTMLInputElement | null>(null);

const businessToday = ref('');
const businessDateError = 'Could not load the business date. Refresh and try again.';
const form = reactive<ExpenseInput>(emptyForm());

const drawerTitle = computed(() => (editing.value ? 'Edit expense' : 'New expense'));
const editingExpense = computed(() =>
  editing.value ? expenses.value.find((e) => e.id === originalCode.value) ?? null : null,
);

// ----- Filters, kept in the address -----

const sortOptions = [
  { key: 'date-desc', label: 'Newest first' },
  { key: 'date-asc', label: 'Oldest first' },
  { key: 'amount-desc', label: 'Largest amount' },
  { key: 'amount-asc', label: 'Smallest amount' },
];

const firstDirection: Record<string, SortDirection> = { date: 'desc', amount: 'desc' };

const filters = useQueryState(
  { q: '', period: 'all', from: '', to: '', category: '', pending: '', voided: '', sort: 'date-desc' },
  {
    period: isPeriodKey,
    from: isIsoDate,
    to: isIsoDate,
    category: (value) => value.length <= 60,
    pending: (value) => value === '1',
    voided: (value) => value === '1',
    sort: (value) => sortOptions.some((option) => option.key === value),
  },
);

const periodKey = computed<PeriodKey>({
  get: () => (isPeriodKey(filters.period) ? filters.period : 'all'),
  set: (value) => {
    filters.period = value;
    if (value !== 'custom') {
      filters.from = '';
      filters.to = '';
    }
  },
});

const pendingOnly = computed(() => filters.pending === '1');
const showVoided = computed(() => filters.voided === '1');
const stacked = useMediaQuery('(max-width: 860px)');

const range = computed(() => resolvePeriod(periodKey.value, businessToday.value, filters.from, filters.to));

const exportHref = computed(() => {
  const params = new URLSearchParams();
  if (range.value?.from) params.set('from', range.value.from);
  if (range.value?.to) params.set('to', range.value.to);
  const query = params.toString();
  return '/api/export/expenses.csv' + (query ? '?' + query : '');
});

const searched = computed(() => {
  const term = filters.q.trim().toLowerCase();
  return expenses.value.filter((e) => {
    if (!inRange(e.date, range.value)) return false;
    if (!term) return true;
    return [e.category, e.relatedPackage, e.notes, e.id].some((field) => (field ?? '').toLowerCase().includes(term));
  });
});

const voidedInScope = computed(() => searched.value.filter((e) => e.voided).length);
const counted = computed(() => searched.value.filter((e) => !e.voided));
const pendingInScope = computed(() => counted.value.filter((e) => e.paymentStatus === 'Pending').length);

// Shares are worked out before the category filter, so picking a category does not make it 100%.
const categoryTotals = computed(() => {
  const pool = pendingOnly.value ? counted.value.filter((e) => e.paymentStatus === 'Pending') : counted.value;
  const totals = new Map<string, number>();
  for (const e of pool) totals.set(e.category, (totals.get(e.category) ?? 0) + e.amount);
  const sum = Array.from(totals.values()).reduce((a, b) => a + b, 0);
  const order = (category: string) => {
    const index = CATEGORIES.indexOf(category);
    return index < 0 ? CATEGORIES.length : index;
  };
  return Array.from(totals.entries())
    .filter(([, total]) => total > 0)
    .sort(([a], [b]) => order(a) - order(b) || a.localeCompare(b))
    .map(([category, total]) => ({
      category,
      total,
      share: sum > 0 ? Math.round((total / sum) * 100) : 0,
      color: CATEGORY_COLORS[CATEGORIES.indexOf(category)] ?? OTHER_COLOR,
    }));
});

const categoryDescription = computed(() =>
  'Spending by category: ' + categoryTotals.value.map((c) => `${c.category} ${c.share}%`).join(', '),
);

const matching = computed(() =>
  searched.value.filter((e) => {
    if (e.voided && !showVoided.value) return false;
    if (pendingOnly.value && (e.voided || e.paymentStatus !== 'Pending')) return false;
    if (filters.category && e.category !== filters.category) return false;
    return true;
  }),
);

// Voided expenses can be listed, but they never count towards a total.
const active = computed(() => matching.value.filter((e) => !e.voided));
const activeTotal = computed(() => active.value.reduce((sum, e) => sum + e.amount, 0));
const activePending = computed(() => active.value.filter((e) => e.paymentStatus === 'Pending'));
const activePendingTotal = computed(() => activePending.value.reduce((sum, e) => sum + e.amount, 0));

const sorted = computed(() => {
  const { field, direction } = sortParts(filters.sort);
  const sign = direction === 'asc' ? 1 : -1;
  return [...matching.value].sort((a, b) => {
    let order = field === 'amount' ? a.amount - b.amount : 0;
    if (order === 0) order = a.date < b.date ? -1 : a.date > b.date ? 1 : a.backendId - b.backendId;
    return order * sign;
  });
});

const { visible, total, shown, loadMore, showAll } = usePaged(sorted, PAGE);

const byDate = computed(() => sortParts(filters.sort).field === 'date');

interface MonthTotals {
  count: number;
  total: number;
  pending: number;
}

function totalsOf(items: ExpenseRow[]): MonthTotals {
  const live = items.filter((e) => !e.voided);
  return {
    count: live.length,
    total: live.reduce((sum, e) => sum + e.amount, 0),
    pending: live.filter((e) => e.paymentStatus === 'Pending').length,
  };
}

const sections = computed(() =>
  byDate.value
    ? monthSections(visible.value, sorted.value, (e) => e.date, totalsOf)
    : singleSection<ExpenseRow, MonthTotals>(visible.value),
);

const isFiltered = computed(
  () =>
    filters.q.trim() !== '' ||
    periodKey.value !== 'all' ||
    filters.category !== '' ||
    pendingOnly.value ||
    showVoided.value,
);

const scopeText = computed(() => {
  const parts = [range.value ? describeRange(range.value) : 'All dates'];
  if (filters.category) parts.push(filters.category);
  if (pendingOnly.value) parts.push('pending only');
  const term = filters.q.trim();
  if (term) parts.push(`matching “${term}”`);
  const listedVoided = matching.value.length - active.value.length;
  if (listedVoided > 0) parts.push(`${listedVoided} voided listed, not counted`);
  return parts.join(' · ');
});

function sortBy(field: string) {
  filters.sort = nextSort(filters.sort, field, firstDirection[field] ?? 'desc');
}

/** Shows one month of spending, as the forecast band's buttons ask for. */
function showMonth(from: string, to: string) {
  filters.period = 'custom';
  filters.from = from;
  filters.to = to;
  filters.category = '';
}

function clearFilters() {
  filters.q = '';
  periodKey.value = 'all';
  filters.category = '';
  filters.pending = '';
  filters.voided = '';
}

function dateLabel(date: string): string {
  return byDate.value ? formatDay(date) : formatDate(date);
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

// ----- The expense form -----

function emptyForm(): ExpenseInput {
  return {
    id: '',
    date: businessToday.value,
    category: 'Tour operations',
    amount: 0,
    relatedPackage: '',
    paymentStatus: 'Pending',
    notes: '',
  };
}

function resetVoid() {
  askVoid.value = false;
  voidReason.value = '';
  voidError.value = '';
}

function openForm() {
  if (!businessToday.value) {
    formError.value = businessDateError;
    toast.error(businessDateError);
    return;
  }
  Object.assign(form, emptyForm());
  form.id = 'EXP-' + (3100 + expenses.value.length + 1);
  editing.value = false;
  originalCode.value = '';
  formError.value = '';
  resetVoid();
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
  resetVoid();
  showForm.value = true;
}

async function startVoid() {
  askVoid.value = true;
  voidError.value = '';
  await nextTick();
  voidPanel.value?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  reasonInput.value?.focus();
}

async function confirmVoid() {
  if (voiding.value || !voidReason.value) return;
  voiding.value = true;
  voidError.value = '';
  try {
    await api.voidExpense(originalCode.value, voidReason.value);
    await load(false);
    historyVersion.value += 1;
    resetVoid();
    toast.success('Expense ' + originalCode.value + ' voided');
  } catch (e) {
    voidError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not void the expense.';
  } finally {
    voiding.value = false;
  }
}

async function restoreExpense() {
  if (restoring.value) return;
  restoring.value = true;
  formError.value = '';
  try {
    await api.restoreExpense(originalCode.value);
    await load(false);
    historyVersion.value += 1;
    toast.success('Expense ' + originalCode.value + ' restored');
  } catch (e) {
    formError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not restore the expense.';
  } finally {
    restoring.value = false;
  }
}

function validate(): string {
  if (!form.date) return 'Please choose the date of this expense.';
  if (!form.category) return 'Please choose a category.';
  if (typeof form.amount !== 'number' || Number.isNaN(form.amount) || form.amount < 0) {
    return 'Please enter an amount of zero or more.';
  }
  return '';
}

async function load(showErrorToast = true): Promise<boolean> {
  try {
    const config = await api.appConfig();
    businessToday.value = config.today;
    expenses.value = await api.expenses();
    return true;
  } catch {
    if (showErrorToast) {
      if (!businessToday.value) toast.error(businessDateError);
      toast.error('We could not load the expenses. Please refresh and try again.');
    }
    return false;
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
    historyVersion.value += 1;
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

const loadError = ref(false);

/** The first load failed: say so, rather than showing an empty list as if there were no records. */
async function retryLoad() {
  loadError.value = false;
  loading.value = true;
  if (!(await load())) loadError.value = true;
}

onMounted(async () => {
  if (!(await load())) {
    loadError.value = true;
    return;
  }
  // Home's "Add an expense" arrives with ?new=1; open the form once the business date is known.
  if (route.query.new === '1') {
    openForm();
    void router.replace({ query: { ...route.query, new: undefined } });
  }
});
</script>

<style scoped>
.form-fieldset {
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}

.form-history {
  margin-top: 28px;
  padding-top: 20px;
  border-top: 1px solid var(--color-border-subtle);
}

.voided-notice {
  display: flex;
  gap: 12px;
  margin-bottom: var(--space-4);
  padding: 12px 14px;
  border: 1px solid var(--tone-danger-border);
  border-radius: var(--radius-md);
  background: var(--tone-danger-surface);
  color: var(--tone-danger-ink);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.voided-notice-icon {
  margin-top: 2px;
  font-size: 18px;
}

.voided-notice-title {
  font-weight: 600;
}

.void-panel {
  margin-bottom: var(--space-4);
}

/* Where the money goes: one bar split by category, and the same categories as filters. */
.category-split {
  display: grid;
  gap: 10px;
}

.category-bar {
  display: flex;
  gap: 2px;
  height: 10px;
  overflow: hidden;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
}

.category-bar-part {
  flex-basis: 0;
  min-width: 4px;
  transition: opacity var(--transition-normal);
}

.category-bar-part.dim {
  opacity: 0.22;
}

.category-swatch {
  width: 9px;
  height: 9px;
  border-radius: 2px;
}

.category-amount {
  color: var(--color-text-primary);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.related-cell {
  max-width: 220px;
  color: var(--color-text-secondary);
}

.notes-cell {
  max-width: 260px;
  overflow: hidden;
  color: var(--color-text-muted);
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
