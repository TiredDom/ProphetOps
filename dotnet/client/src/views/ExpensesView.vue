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
      <button class="primary-button" type="button" aria-keyshortcuts="N" @click="openForm">
        <UiIcon name="plus" />
        Add expense
      </button>
    </template>

    <section class="dss-page">
      <ExpenseDetail
        :open="detailOpen"
        :expense="detailExpense"
        :can-see-history="canSeeHistory"
        :history-version="historyVersion"
        :busy="detailBusy"
        :void-error="detailVoidError"
        :action-error="detailActionError"
        @close="detailOpen = false"
        @edit="editFromDetail"
        @mark-paid="markPaidFromDetail"
        @void="voidFromDetail"
        @restore="restoreFromDetail"
      />

      <Drawer :open="showForm" :title="drawerTitle" :dirty="formDirty" @close="closeForm">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <p v-if="!editing && draftRestoredAt" class="drawer-form-merge" role="status">
          Restored the expense you started {{ draftStartedLabel() }}; nothing was lost.
          <button type="button" class="field-note-action" @click="startOver">Start over</button>
        </p>
        <FormProblems :problems="attempted ? problems : []" @focus="focusField" />

        <div class="form-grid">
          <FormField v-if="editing" id="expense-code" label="Expense ID" wide v-slot="f">
            <input :id="f.id" :value="form.id" type="text" readonly />
          </FormField>

          <FormField :id="fieldId('date')" label="Date" :error="errors.date" :note="dateNote" v-slot="f">
            <input
              :id="f.id"
              v-model="form.date"
              type="date"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @blur="touch('date')"
            />
          </FormField>

          <FormField :id="fieldId('category')" label="Category" :error="errors.category" v-slot="f">
            <select
              :id="f.id"
              v-model="form.category"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
            >
              <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
            </select>
          </FormField>

          <FormField :id="fieldId('amount')" label="Amount" :error="errors.amount" :note="amountNote" v-slot="f">
            <MoneyField
              :id="f.id"
              v-model="form.amount"
              :invalid="f.invalid"
              :described-by="f.describedBy"
              @blur="touch('amount')"
            />
          </FormField>

          <FormField
            :id="fieldId('paymentStatus')"
            label="Payment status"
            hint="Pending counts as still owed"
            v-slot="f"
          >
            <select :id="f.id" v-model="form.paymentStatus" :aria-describedby="f.describedBy">
              <option value="Paid">Paid</option>
              <option value="Pending">Pending</option>
            </select>
          </FormField>

          <FormField
            :id="fieldId('relatedPackage')"
            label="Related package or operation"
            optional
            wide
            hint="Pick a package name to keep its costs together in Reports"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model.trim="form.relatedPackage"
              maxlength="120"
              list="expense-related"
              autocomplete="off"
              :aria-describedby="f.describedBy"
            />
            <datalist id="expense-related">
              <option v-for="name in relatedNames" :key="name" :value="name"></option>
            </datalist>
          </FormField>

          <FormField :id="fieldId('notes')" label="Notes" optional wide v-slot="f">
            <input
              :id="f.id"
              v-model.trim="form.notes"
              maxlength="240"
              placeholder="Supplier, receipt number, what it was for"
              :aria-describedby="f.describedBy"
            />
          </FormField>
        </div>

        <template #footer="{ requestClose }">
          <button class="secondary-button" type="button" :disabled="saving" @click="requestClose">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : editing ? 'Save changes' : 'Save expense' }}
          </button>
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
          <button
            v-if="stacked"
            type="button"
            class="filter-chip select-toggle"
            :aria-pressed="selecting"
            @click="toggleSelecting"
          >
            {{ selecting ? 'Done selecting' : 'Select' }}
          </button>
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
                <button
                  class="record-item"
                  :class="{ 'is-voided': e.voided, selectable: selecting, 'is-selected': selected.has(e.id) }"
                  type="button"
                  :aria-pressed="selecting ? selected.has(e.id) : undefined"
                  :disabled="selecting && e.voided"
                  @click="selecting ? toggle(e) : openDetail(e)"
                >
                  <span v-if="selecting" class="record-check" aria-hidden="true">
                    <UiIcon v-if="selected.has(e.id)" name="check" :stroke-width="2.6" />
                  </span>
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
                  <th scope="col" class="select-cell">
                    <input
                      type="checkbox"
                      :checked="allShownSelected"
                      :indeterminate.prop="someShownSelected && !allShownSelected"
                      :disabled="selectableShown.length === 0"
                      :aria-label="`Select all ${selectableShown.length} expenses shown`"
                      @change="toggleAllShown"
                    />
                  </th>
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
                  <th scope="rowgroup" colspan="5">
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
                  :class="{ 'is-voided': e.voided, 'is-selected': selected.has(e.id) }"
                  @click="openDetail(e)"
                >
                  <td class="select-cell" @click.stop>
                    <input
                      type="checkbox"
                      :checked="selected.has(e.id)"
                      :disabled="e.voided"
                      :aria-label="`Select expense ${e.id}, ${e.category}, ${peso(e.amount)}`"
                      @click="onCheck(e, $event)"
                    />
                  </td>
                  <td class="cell-date">{{ dateLabel(e.date) }}</td>
                  <td>
                    <button class="row-open" type="button" @click.stop="openDetail(e)">{{ e.category }}</button>
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

          <SelectionBar :count="selected.size" :detail="selectionDetail" @clear="clearSelection">
            <button
              class="primary-button compact-button"
              type="button"
              :disabled="bulkBusy || selectedPending.length === 0"
              @click="bulkMarkPaid"
            >
              <UiIcon name="wallet" />
              {{ bulkBusy ? 'Saving…' : selectedPending.length ? `Mark ${selectedPending.length} as paid` : 'All paid' }}
            </button>
          </SelectionBar>
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
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import ListFooter from '../components/ListFooter.vue';
import SelectionBar from '../components/SelectionBar.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import SortHeader from '../components/SortHeader.vue';
import UiIcon from '../components/UiIcon.vue';
import MoneyField from '../components/MoneyField.vue';
import FormField from '../components/FormField.vue';
import FormProblems from '../components/FormProblems.vue';
import ExpenseDetail, { type ExpenseBusy } from '../components/ExpenseDetail.vue';
import SpendingGuide from '../components/forecast/SpendingGuide.vue';
import { usePaged } from '../composables/usePaged';
import { undoAction, useToast } from '../composables/useToast';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useQueryState } from '../composables/useQueryState';
import { useFieldErrors } from '../composables/useFieldErrors';
import { useFormDraft } from '../composables/useDraft';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { api, ApiError, type ExpenseRow, type ExpenseInput } from '../api';
import { count, peso, pesoCompact, stateLabel } from '../format';
import { describeRange, formatDate, formatDay, inRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';
import { monthSections, nextSort, singleSection, sortParts, type SortDirection } from '../listing';
import { isAfter, nextCode, suggestions } from '../records';
import { rememberRecent } from '../recents';

const PAGE = 50;
const CATEGORIES = ['Tour operations', 'Marketing', 'Seasonal cost', 'Overhead'];
// One navy family, darkest for the first category, so the split reads as shares of one whole.
const CATEGORY_COLORS = ['var(--color-series-1)', 'var(--color-series-2)', 'var(--color-series-3)', 'var(--color-series-4)'];
const OTHER_COLOR = 'var(--color-series-other)';

const toast = useToast();
const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();

const expenses = ref<ExpenseRow[]>([]);
const packageNames = ref<string[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');
const historyVersion = ref(0);
const canSeeHistory = computed(() => canAccess(auth.user?.role, '/reports'));

const businessToday = ref('');
const businessDateError = 'Could not load the business date. Refresh and try again.';
const form = reactive<ExpenseInput>(emptyForm());

const drawerTitle = computed(() => (editing.value ? 'Edit expense' : 'New expense'));

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

function toInput(e: ExpenseRow): ExpenseInput {
  return {
    id: e.id,
    date: e.date,
    category: e.category,
    amount: e.amount,
    relatedPackage: e.relatedPackage,
    paymentStatus: e.paymentStatus,
    notes: e.notes ?? '',
  };
}

/** Whether a record is still as a change left it; an undo never overwrites someone else's edit. */
function unchangedSince(after: ExpenseRow): boolean {
  const now = expenses.value.find((e) => e.id === after.id);
  return !!now && JSON.stringify(toInput(now)) === JSON.stringify(toInput(after)) && !!now.voided === !!after.voided;
}

// ----- Selecting several expenses -----

const selected = reactive(new Set<string>());
const selecting = ref(false);
const bulkBusy = ref(false);
let lastChecked: string | null = null;

const selectableShown = computed(() => visible.value.filter((e) => !e.voided));
const allShownSelected = computed(() => selectableShown.value.length > 0 && selectableShown.value.every((e) => selected.has(e.id)));
const someShownSelected = computed(() => selectableShown.value.some((e) => selected.has(e.id)));
const selectedExpenses = computed(() => expenses.value.filter((e) => selected.has(e.id) && !e.voided));
const selectedPending = computed(() => selectedExpenses.value.filter((e) => e.paymentStatus !== 'Paid'));
const selectionDetail = computed(() => `${peso(selectedExpenses.value.reduce((sum, e) => sum + e.amount, 0))} in all`);

function toggle(e: ExpenseRow) {
  if (e.voided) return;
  if (selected.has(e.id)) selected.delete(e.id);
  else selected.add(e.id);
  lastChecked = e.id;
}

/** Shift-click selects every expense between the last one ticked and this one. */
function onCheck(e: ExpenseRow, event: MouseEvent) {
  const order = visible.value;
  const from = lastChecked ? order.findIndex((item) => item.id === lastChecked) : -1;
  const to = order.findIndex((item) => item.id === e.id);
  if (event.shiftKey && from >= 0 && to >= 0) {
    const turnOn = !selected.has(e.id);
    for (const item of order.slice(Math.min(from, to), Math.max(from, to) + 1)) {
      if (item.voided) continue;
      if (turnOn) selected.add(item.id);
      else selected.delete(item.id);
    }
    lastChecked = e.id;
    return;
  }
  toggle(e);
}

function toggleAllShown() {
  if (allShownSelected.value) for (const e of selectableShown.value) selected.delete(e.id);
  else for (const e of selectableShown.value) selected.add(e.id);
}

function clearSelection() {
  selected.clear();
  lastChecked = null;
}

function toggleSelecting() {
  selecting.value = !selecting.value;
  if (!selecting.value) clearSelection();
}

// A filter that hides an expense also lets go of it, so nothing unseen is changed.
watch(matching, (list) => {
  const still = new Set(list.map((e) => e.id));
  for (const id of [...selected]) if (!still.has(id)) selected.delete(id);
});

/** Marks each selected pending expense as paid, one at a time, and says plainly if any failed. */
async function bulkMarkPaid() {
  if (bulkBusy.value) return;
  const targets = selectedPending.value.map((e) => ({ ...e }));
  if (targets.length === 0) return;
  bulkBusy.value = true;
  const done: { before: ExpenseRow; after: ExpenseRow }[] = [];
  const failed: string[] = [];
  for (const e of targets) {
    try {
      done.push({ before: e, after: await api.updateExpense(e.id, { ...toInput(e), paymentStatus: 'Paid' }) });
    } catch {
      failed.push(e.id);
    }
  }
  await load(false);
  historyVersion.value += 1;
  clearSelection();
  selecting.value = false;
  bulkBusy.value = false;
  if (done.length > 0) {
    const noun = done.length === 1 ? 'expense' : 'expenses';
    toast.success(`${done.length} ${noun} marked as paid`, {
      action: undoAction(async () => {
        for (const step of done) await undoTo(step.before, step.after, '', true);
        await load(false);
        historyVersion.value += 1;
        toast.success(`${done.length} ${noun} pending again`);
      }),
    });
  }
  if (failed.length > 0) {
    toast.error(`Could not mark ${failed.join(', ')} as paid. Check the connection, then try again.`);
  }
}

// ----- The expense panel -----

const detailOpen = ref(false);
const detailCode = ref<string | null>(null);
const detailExpense = computed(() => expenses.value.find((e) => e.id === detailCode.value) ?? null);
const detailBusy = ref<ExpenseBusy>('');
const detailVoidError = ref('');
const detailActionError = ref('');
// Set when the form was opened from an expense's panel, so closing the form goes back to it.
const returnToDetail = ref(false);

function openDetail(e: ExpenseRow) {
  detailCode.value = e.id;
  detailVoidError.value = '';
  detailActionError.value = '';
  detailOpen.value = true;
  rememberRecent({ kind: 'expense', code: e.id, label: `${e.category} · ${peso(e.amount)}`, detail: `${e.id} · ${formatDate(e.date)}` });
}

function editFromDetail() {
  const e = detailExpense.value;
  if (!e) return;
  detailOpen.value = false;
  openEdit(e);
  returnToDetail.value = true;
}

async function undoTo(before: ExpenseRow, after: ExpenseRow, done: string, quiet = false) {
  if (!(await load(false)) || !unchangedSince(after)) {
    toast.error(`Expense ${before.id} changed after that, so the change could not be undone. Open it to change it by hand.`);
    return;
  }
  try {
    if (!!before.voided !== !!after.voided) {
      if (before.voided) await api.voidExpense(before.id, before.voidReason || 'Voided again after a restore');
      else await api.restoreExpense(before.id);
    } else {
      await api.updateExpense(before.id, toInput(before));
    }
    if (quiet) return;
    await load(false);
    historyVersion.value += 1;
    toast.success(done);
  } catch (e) {
    await load(false);
    toast.error(e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not undo the change. Check the connection, then change the expense by hand.');
  }
}

async function markPaidFromDetail() {
  const e = detailExpense.value;
  if (!e || detailBusy.value) return;
  detailBusy.value = 'paid';
  detailActionError.value = '';
  const before = { ...e };
  try {
    const after = await api.updateExpense(e.id, { ...toInput(e), paymentStatus: 'Paid' });
    await load(false);
    historyVersion.value += 1;
    toast.success(`Expense ${e.id} marked as paid`, {
      action: undoAction(() => undoTo(before, after, `Expense ${e.id} is pending again`)),
    });
  } catch (err) {
    detailActionError.value = err instanceof ApiError ? Object.values(err.fields)[0] ?? err.message : 'Could not update the expense. Check the connection, then try again.';
  } finally {
    detailBusy.value = '';
  }
}

async function voidFromDetail(reason: string) {
  const e = detailExpense.value;
  if (!e || detailBusy.value || !reason) return;
  detailBusy.value = 'void';
  detailVoidError.value = '';
  const before = { ...e };
  try {
    const after = await api.voidExpense(e.id, reason);
    await load(false);
    historyVersion.value += 1;
    toast.success(`Expense ${e.id} voided`, {
      action: undoAction(() => undoTo(before, after, `Expense ${e.id} restored`)),
    });
  } catch (err) {
    detailVoidError.value = err instanceof ApiError ? Object.values(err.fields)[0] ?? err.message : 'Could not void the expense. Check the connection, then try again.';
  } finally {
    detailBusy.value = '';
  }
}

async function restoreFromDetail() {
  const e = detailExpense.value;
  if (!e || detailBusy.value) return;
  detailBusy.value = 'restore';
  detailActionError.value = '';
  const before = { ...e };
  try {
    const after = await api.restoreExpense(e.id);
    await load(false);
    historyVersion.value += 1;
    toast.success(`Expense ${e.id} restored`, {
      action: undoAction(() => undoTo(before, after, `Expense ${e.id} voided again`)),
    });
  } catch (err) {
    detailActionError.value = err instanceof ApiError ? Object.values(err.fields)[0] ?? err.message : 'Could not restore the expense. Check the connection, then try again.';
  } finally {
    detailBusy.value = '';
  }
}

// ----- The expense form -----

const FIELD_LABELS = {
  date: 'Date',
  category: 'Category',
  amount: 'Amount',
  paymentStatus: 'Payment status',
  relatedPackage: 'Related package or operation',
  notes: 'Notes',
} as const;
type ExpenseField = keyof typeof FIELD_LABELS;

const openedSnapshot = ref('');

const {
  errors,
  attempted,
  problems,
  touch,
  recheck,
  validateAll,
  placeServerErrors,
  reset: resetFieldErrors,
  fieldId,
  focusField,
} = useFieldErrors<ExpenseField>('expense', FIELD_LABELS, {
  date: () => (form.date ? '' : 'Choose the date of the expense.'),
  category: () => (form.category ? '' : 'Choose a category.'),
  amount: () => (Number.isFinite(form.amount) && form.amount > 0 ? '' : 'Enter the amount spent, more than ₱0.'),
});

watch(form, recheck);

const dateNote = computed(() =>
  isAfter(form.date, businessToday.value) ? 'This is after today. Check the date, or record it when it is paid.' : '',
);

/** An amount far above this category's usual is worth a second look before it skews Reports. */
const amountNote = computed(() => {
  const amounts = expenses.value
    .filter((e) => !e.voided && e.category === form.category && e.id !== originalCode.value)
    .map((e) => e.amount)
    .sort((a, b) => a - b);
  if (amounts.length < 8 || form.amount <= 0) return '';
  const middle = amounts[Math.floor(amounts.length / 2)];
  if (middle <= 0 || form.amount < middle * 10) return '';
  return `${peso(form.amount)} is more than ten times the usual ${form.category.toLowerCase()} expense (about ${peso(middle)}). Check for a stray digit.`;
});

const relatedNames = computed(() => suggestions([...packageNames.value, ...expenses.value.map((e) => e.relatedPackage)]));

function snapshotForm(): string {
  return JSON.stringify({ ...form });
}

const formDirty = computed(() => showForm.value && snapshotForm() !== openedSnapshot.value);

// A new expense's unfinished form survives a reload or a closed tab.
const {
  restoredAt: draftRestoredAt,
  restore: restoreDraft,
  discard: discardDraft,
  startedLabel: draftStartedLabel,
} = useFormDraft<ExpenseInput>('expense', {
  active: () => showForm.value && !editing.value,
  dirty: () => formDirty.value,
  snapshot: () => ({ ...form }),
});

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

function openForm() {
  if (!businessToday.value) {
    formError.value = businessDateError;
    toast.error(businessDateError);
    return;
  }
  resetNewForm();
  editing.value = false;
  originalCode.value = '';
  formError.value = '';
  resetFieldErrors();
  returnToDetail.value = false;
  detailOpen.value = false;
  openedSnapshot.value = snapshotForm();
  // An unfinished expense from earlier comes back, under a new ID in case its ID was taken since.
  const kept = restoreDraft();
  if (kept) Object.assign(form, kept, { id: form.id });
  showForm.value = true;
}

function resetNewForm() {
  Object.assign(form, emptyForm());
  form.id = nextCode('EXP', expenses.value.map((e) => e.id), 3101);
}

/** Drops a restored draft and starts the form afresh. */
function startOver() {
  discardDraft();
  resetNewForm();
  resetFieldErrors();
  openedSnapshot.value = snapshotForm();
}

function openEdit(expense: ExpenseRow) {
  Object.assign(form, toInput(expense));
  editing.value = true;
  originalCode.value = expense.id;
  formError.value = '';
  resetFieldErrors();
  openedSnapshot.value = snapshotForm();
  showForm.value = true;
}

function closeForm() {
  // Closing a new expense's form on purpose lets its draft go.
  if (!editing.value) discardDraft();
  showForm.value = false;
  if (returnToDetail.value && detailExpense.value) detailOpen.value = true;
  returnToDetail.value = false;
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

/** Package names for the related-package suggestions; the form works without them. */
async function loadPackageNames() {
  try {
    packageNames.value = (await api.packages()).map((p) => p.packageName);
  } catch {
    packageNames.value = [];
  }
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  const found = validateAll();
  if (found.length > 0) {
    void focusField(found[0].key);
    return;
  }

  saving.value = true;
  const wasEditing = editing.value;
  const before = wasEditing ? expenses.value.find((e) => e.id === originalCode.value) ?? null : null;
  try {
    const saved = wasEditing
      ? await api.updateExpense(originalCode.value, { ...form })
      : await api.createExpense({ ...form });
    await load();
    historyVersion.value += 1;
    showForm.value = false;
    if (wasEditing && before) {
      const snapshot = { ...before };
      toast.success(`Expense ${saved.id} saved`, {
        action: undoAction(() => undoTo(snapshot, saved, `Expense ${saved.id} is back as it was`)),
      });
      detailCode.value = saved.id;
      if (returnToDetail.value) detailOpen.value = true;
    } else {
      discardDraft();
      toast.success(`Expense ${saved.id} added`, {
        action: {
          label: 'Open',
          run: () => {
            detailCode.value = saved.id;
            detailOpen.value = true;
          },
        },
      });
    }
    returnToDetail.value = false;
  } catch (e) {
    if (e instanceof ApiError) {
      const unplaced = placeServerErrors(e.fields, {});
      if (problems.value.length > 0) void focusField(problems.value[0].key);
      formError.value = unplaced[0] ?? (problems.value.length > 0 ? '' : e.message);
    } else {
      formError.value = 'Could not reach the server, so nothing was saved. Your entries are still here; check the connection and save again.';
    }
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

/** A link such as /expenses?open=EXP-3104 (from search or another page) opens that expense. */
function openFromAddress() {
  const code = typeof route.query.open === 'string' ? route.query.open : '';
  if (!code) return;
  const found = expenses.value.find((e) => e.id.toLowerCase() === code.toLowerCase());
  if (found) openDetail(found);
  void router.replace({ query: { ...route.query, open: undefined } });
}

onMounted(async () => {
  void loadPackageNames();
  if (!(await load())) {
    loadError.value = true;
    return;
  }
  openFromAddress();
  // Home's "Add an expense" arrives with ?new=1; open the form once the business date is known.
  if (route.query.new === '1') {
    openForm();
    void router.replace({ query: { ...route.query, new: undefined } });
  }
});

watch(() => route.query.open, (value) => {
  if (value && !loading.value) openFromAddress();
});
</script>

<style scoped>
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
