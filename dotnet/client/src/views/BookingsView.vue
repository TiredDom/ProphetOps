<template>
  <AppShell title="Bookings">
    <template #actions>
      <ActionMenu :items="menuItems" />
      <button class="primary-button" type="button" @click="openForm">
        <UiIcon name="plus" />
        Add booking
      </button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="drawerTitle" @close="closeForm">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>

        <p v-if="unusualWarning" class="drawer-form-check" role="alert">{{ unusualWarning }}</p>

        <fieldset class="booking-mode">
          <legend class="section-label">Booking type</legend>
          <div class="booking-mode-options" role="radiogroup" aria-label="Booking type">
            <label class="booking-mode-option" :class="{ selected: form.mode === 'package' }">
              <input
                type="radio"
                name="booking-mode"
                value="package"
                :checked="form.mode === 'package'"
                @change="setMode('package')"
              />
              <span>Ready-made package</span>
            </label>
            <label class="booking-mode-option" :class="{ selected: form.mode === 'custom' }">
              <input
                type="radio"
                name="booking-mode"
                value="custom"
                :checked="form.mode === 'custom'"
                @change="setMode('custom')"
              />
              <span>Tailored (custom) trip</span>
            </label>
          </div>
        </fieldset>

        <div class="form-grid">
          <label v-if="editing" class="account-field field-wide">
            <span>Booking ID</span>
            <input v-model="form.id" type="text" readonly />
          </label>
          <label class="account-field">
            <span>Booking date</span>
            <input v-model="form.ds" type="date" />
          </label>
          <label class="account-field">
            <span>Passengers</span>
            <input v-model.number="form.y" type="number" min="1" @input="applyPackage" />
          </label>
          <label class="account-field field-wide">
            <span>Client / agency partner</span>
            <input v-model.trim="form.client" maxlength="120" />
          </label>

          <label v-if="form.mode === 'package'" class="account-field field-wide">
            <span>Package</span>
            <select v-model="form.packageId" @change="applyPackage">
              <option v-for="p in packages" :key="p.id" :value="p.id">{{ p.packageName }}</option>
            </select>
          </label>

          <template v-else>
            <label class="account-field field-wide">
              <span>Trip / quotation name</span>
              <input v-model.trim="form.package" maxlength="120" placeholder="e.g. Coron island-hopping getaway" />
            </label>
            <label class="account-field field-wide">
              <span>Destination</span>
              <input v-model.trim="form.destination" maxlength="120" placeholder="e.g. Coron, Palawan" />
            </label>
          </template>

          <label class="account-field">
            <span>Price</span>
            <MoneyField v-model="form.grossRevenue" />
          </label>
          <label class="account-field">
            <span>Payment status</span>
            <select v-model="form.paymentStatus">
              <option>Paid</option>
              <option>Partially Paid</option>
              <option>Pending</option>
            </select>
          </label>
          <label class="account-field field-wide">
            <span>Booking status</span>
            <select v-model="form.bookingStatus">
              <option>Confirmed</option>
              <option>Reserved</option>
              <option>Pending</option>
            </select>
          </label>
          <label class="account-field field-wide">
            <span>Notes (optional)</span>
            <input v-model.trim="form.notes" maxlength="240" placeholder="Anything the team should know" />
          </label>
        </div>

        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="closeForm">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : editing ? 'Save changes' : 'Save booking' }}
          </button>
        </template>
      </Drawer>

      <Drawer :open="showImport" title="Import bookings from a file" @close="showImport = false">
        <p class="import-hint">
          Export the sheet as a CSV file with the columns date, client, package, destination,
          passengers, and revenue. Code, payment, status, staff, and notes are optional, and
          extra columns are ignored.
        </p>

        <p v-if="importError" class="drawer-form-error" role="alert">{{ importError }}</p>

        <div class="import-pick">
          <input
            ref="importFileInput"
            class="visually-hidden"
            type="file"
            accept=".csv,text/csv"
            @change="pickImportFile"
          />
          <button class="secondary-button" type="button" :disabled="importBusy" @click="importFileInput?.click()">
            {{ importFile ? 'Choose a different file' : 'Choose CSV file' }}
          </button>
          <span v-if="importFile" class="import-filename">{{ importFile.name }}</span>
        </div>

        <template v-if="importPreviewData">
          <dl class="import-summary">
            <div>
              <dt>Will be imported</dt>
              <dd>{{ count(importable) }}</dd>
            </div>
            <div v-if="importPreviewData.duplicates">
              <dt>Already in the system</dt>
              <dd>{{ count(importPreviewData.duplicates) }}</dd>
            </div>
            <div v-if="importPreviewData.skipped">
              <dt>Rows that could not be read</dt>
              <dd>{{ count(importPreviewData.skipped) }}</dd>
            </div>
            <div v-if="importPreviewData.from">
              <dt>Covers</dt>
              <dd>
                {{ importPreviewData.from }} to {{ importPreviewData.to }}
                ({{ importPreviewData.months }} month{{ importPreviewData.months === 1 ? '' : 's' }})
              </dd>
            </div>
            <div>
              <dt>Passengers</dt>
              <dd>{{ count(importPreviewData.passengers) }}</dd>
            </div>
            <div>
              <dt>Total revenue</dt>
              <dd>{{ peso(importPreviewData.totalRevenue) }}</dd>
            </div>
          </dl>

          <div v-if="importPreviewData.warnings.length" class="import-notes import-warnings">
            <p class="import-notes-title">Read with a caution</p>
            <p v-for="w in importPreviewData.warnings.slice(0, 8)" :key="'w' + w.line">
              Line {{ w.line }}: {{ w.reason }}
            </p>
            <p v-if="importPreviewData.warnings.length > 8">
              and {{ importPreviewData.warnings.length - 8 }} more.
            </p>
          </div>

          <div v-if="importPreviewData.problems.length" class="import-notes import-problems">
            <p class="import-notes-title">Rows that will be skipped</p>
            <p v-for="p in importPreviewData.problems.slice(0, 8)" :key="'p' + p.line">
              Line {{ p.line }}: {{ p.reason }}
            </p>
            <p v-if="importPreviewData.problems.length > 8">
              and {{ importPreviewData.problems.length - 8 }} more.
            </p>
          </div>

          <p class="import-hint">
            Nothing is saved until you press import. Imported bookings are recorded as history
            and do not take slots from packages on sale today.
          </p>
        </template>

        <template #footer>
          <button class="secondary-button" type="button" :disabled="importBusy" @click="showImport = false">Cancel</button>
          <button
            class="primary-button"
            type="button"
            :disabled="importBusy || importable === 0"
            @click="commitImport"
          >
            {{ importBusy ? 'Working…' : importable > 0
              ? 'Import ' + count(importable) + ' booking' + (importable === 1 ? '' : 's')
              : 'Nothing to import' }}
          </button>
        </template>
      </Drawer>

      <BookingDetail
        :open="detailOpen"
        :booking="detailBooking"
        :can-see-history="canSeeHistory"
        :history-version="historyVersion"
        :busy="detailBusy"
        :void-error="detailVoidError"
        :action-error="detailActionError"
        @close="detailOpen = false"
        @edit="editFromDetail"
        @mark-paid="quickUpdate('paid')"
        @confirm="quickUpdate('confirm')"
        @void="voidFromDetail"
        @restore="restoreFromDetail"
      />

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="8" />
      </div>

      <EmptyState
        v-else-if="!bookings.length"
        title="No bookings yet"
        message="Record your first booking or quotation to start tracking passengers and revenue."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openForm">Add booking</button>
        </template>
      </EmptyState>

      <section v-else class="content-panel records-panel" aria-label="Bookings">
        <div class="records-tools">
          <SearchField v-model="filters.q" placeholder="Search client, package, destination or ID" />
          <PeriodSelect
            v-model:period="periodKey"
            v-model:from="filters.from"
            v-model:to="filters.to"
            :today="businessToday"
          />
          <label v-if="stacked" class="records-sort">
            <span class="visually-hidden">Sort bookings</span>
            <select v-model="filters.sort">
              <option v-for="option in sortOptions" :key="option.key" :value="option.key">{{ option.label }}</option>
            </select>
          </label>
        </div>

        <div class="records-filters" role="group" aria-label="Filter by payment">
          <button
            v-for="option in paymentOptions"
            :key="option.key"
            type="button"
            class="filter-chip"
            :aria-pressed="filters.payment === option.key"
            @click="filters.payment = option.key"
          >
            {{ option.label }}
            <span class="chip-count">{{ count(paymentCounts[option.key] ?? 0) }}</span>
          </button>
          <template v-if="voidedInScope > 0">
            <span class="chip-divider" aria-hidden="true"></span>
            <button
              type="button"
              class="filter-chip"
              :aria-pressed="showVoided"
              @click="filters.voided = showVoided ? '' : '1'"
            >
              Show voided
              <span class="chip-count">{{ count(voidedInScope) }}</span>
            </button>
          </template>
        </div>

        <p class="records-summary" aria-live="polite">
          <span class="summary-figure"><strong>{{ count(active.length) }}</strong> {{ active.length === 1 ? 'booking' : 'bookings' }}</span>
          <span class="summary-figure"><strong>{{ count(activePassengers) }}</strong> {{ activePassengers === 1 ? 'passenger' : 'passengers' }}</span>
          <span class="summary-figure"><strong>{{ peso(activeRevenue) }}</strong> revenue</span>
          <span v-if="filters.payment === 'all' && unpaidInView.length" class="summary-figure tone-warning">
            <strong>{{ peso(unpaidValue) }}</strong> in {{ count(unpaidInView.length) }} unpaid {{ unpaidInView.length === 1 ? 'booking' : 'bookings' }}
          </span>
          <span class="summary-scope">{{ scopeText }}</span>
          <button v-if="isFiltered" class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </p>

        <template v-if="matching.length">
          <ul v-if="stacked" class="record-list">
            <template v-for="section in sections" :key="section.key">
              <li v-if="section.label && section.summary" class="record-list-month">
                <span>{{ section.label }}</span>
                <span>{{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'booking' : 'bookings' }} · {{ peso(section.summary.revenue) }}</span>
              </li>
              <li v-for="b in section.items" :key="b.id">
                <button
                  class="record-item"
                  :class="{ 'is-voided': b.voided }"
                  type="button"
                  @click="openDetail(b)"
                >
                  <span class="record-item-top">
                    <span class="record-item-title">{{ b.client }}</span>
                    <span class="record-item-figure">{{ peso(b.grossRevenue) }}</span>
                  </span>
                  <span class="record-item-sub">{{ tripLabel(b) }}</span>
                  <span class="record-item-meta">
                    <span>{{ dateLabel(b.ds) }} · {{ b.id }} · {{ b.y }} pax<template v-if="b.voided"> · Voided</template></span>
                    <span class="record-badge" :class="badge(b.paymentStatus)">{{ b.paymentStatus }}</span>
                  </span>
                </button>
              </li>
            </template>
          </ul>

          <div v-else class="dss-table-frame">
            <table class="dss-table records-table">
              <thead>
                <tr>
                  <SortHeader label="Date" field="date" :sort="filters.sort" @sort="sortBy" />
                  <SortHeader label="Client" field="client" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Trip</th>
                  <SortHeader label="Passengers" field="pax" align="right" :sort="filters.sort" @sort="sortBy" />
                  <SortHeader label="Revenue" field="revenue" align="right" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Payment</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody v-for="section in sections" :key="section.key">
                <tr v-if="section.label && section.summary" class="month-row">
                  <th scope="rowgroup" colspan="3">
                    {{ section.label }}
                    <span class="month-count">· {{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'booking' : 'bookings' }}</span>
                  </th>
                  <td class="num">{{ count(section.summary.passengers) }}</td>
                  <td class="num">{{ peso(section.summary.revenue) }}</td>
                  <td colspan="2"></td>
                </tr>
                <tr
                  v-for="b in section.items"
                  :key="b.id"
                  class="record-row"
                  :class="{ 'is-voided': b.voided }"
                  @click="openDetail(b)"
                >
                  <td class="cell-date">{{ dateLabel(b.ds) }}</td>
                  <td>
                    <button class="row-open" type="button" @click.stop="openDetail(b)">{{ b.client }}</button>
                    <span class="row-subtext">
                      {{ b.id }}<span v-if="b.voided" class="voided-tag" :title="b.voidReason ?? ''">Voided</span>
                    </span>
                  </td>
                  <td>
                    <span class="trip-name">{{ b.package }}</span>
                    <span class="row-subtext">
                      {{ b.destination }}<span v-if="isTailored(b)" class="kind-tag">Tailored</span>
                    </span>
                  </td>
                  <td class="num">{{ b.y }}</td>
                  <td class="num"><strong>{{ peso(b.grossRevenue) }}</strong></td>
                  <td><span class="record-badge" :class="badge(b.paymentStatus)">{{ b.paymentStatus }}</span></td>
                  <td><span class="record-badge" :class="badge(b.bookingStatus)">{{ b.bookingStatus }}</span></td>
                </tr>
              </tbody>
            </table>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            :page-size="PAGE"
            noun="bookings"
            @more="loadMore"
            @all="showAll"
          />
        </template>

        <div v-else class="records-empty">
          <strong>No bookings match</strong>
          <span>Try a different search, payment status or period.</span>
          <button class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </div>
      </section>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import ActionMenu, { type ActionMenuItem } from '../components/ActionMenu.vue';
import BookingDetail, { type BookingBusy } from '../components/BookingDetail.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import ListFooter from '../components/ListFooter.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import SortHeader from '../components/SortHeader.vue';
import UiIcon from '../components/UiIcon.vue';
import MoneyField from '../components/MoneyField.vue';
import { usePaged } from '../composables/usePaged';
import { useToast } from '../composables/useToast';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useQueryState } from '../composables/useQueryState';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { api, ApiError, type Booking, type BookingInput, type ImportPreview, type PackageOption } from '../api';
import { count, peso } from '../format';
import { describeRange, formatDate, formatDay, inRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';
import { compareText, monthSections, nextSort, singleSection, sortParts, type SortDirection } from '../listing';

type BookingMode = 'package' | 'custom';
type BookingForm = BookingInput & { mode: BookingMode };
type BookingRecovery =
  | { kind: 'create'; payload: BookingInput }
  | { kind: 'stale-edit'; message: string };

const PAGE = 50;

const toast = useToast();
const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();

const bookings = ref<Booking[]>([]);
const packages = ref<PackageOption[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');
const drawerTitle = ref('New booking');
// Set when the form was opened from a booking's panel, so closing the form goes back to it.
const returnToDetail = ref(false);

const unusualWarning = ref('');
const confirmUnusual = ref(false);

const showImport = ref(false);
const importFileInput = ref<HTMLInputElement | null>(null);
const importFile = ref<File | null>(null);
const importPreviewData = ref<ImportPreview | null>(null);
const importBusy = ref(false);
const importError = ref('');

const importable = computed(() => {
  const p = importPreviewData.value;
  return p ? Math.max(0, p.valid - p.duplicates) : 0;
});

const pendingRecovery = ref<BookingRecovery | null>(null);
const createRecoveryError = 'Could not confirm whether this booking was saved. Check your connection, then try again before changing this form.';
const staleRecoveryError = 'Could not reload the latest booking after a conflict. Check your connection, then try again before saving.';

const businessToday = ref('');
const businessDateError = 'Could not load the business date. Refresh and try again.';
const form = reactive<BookingForm>(blank());

// The booking panel
const detailOpen = ref(false);
const detailCode = ref<string | null>(null);
const detailBooking = computed(() => bookings.value.find((b) => b.id === detailCode.value) ?? null);
const detailBusy = ref<BookingBusy>('');
const detailVoidError = ref('');
const detailActionError = ref('');
const historyVersion = ref(0);
// The change history comes from the activity log, which Owner and Admin can read.
const canSeeHistory = computed(() => canAccess(auth.user?.role, '/reports'));

const menuItems: ActionMenuItem[] = [
  {
    label: 'Export bookings',
    hint: 'Every booking as a CSV file, voided ones marked',
    icon: 'download',
    href: '/api/export/bookings.csv',
  },
  { label: 'Import from a file', hint: 'Add bookings from a CSV sheet', icon: 'upload', run: openImport },
];

// ----- Filters, kept in the address -----

const paymentOptions = [
  { key: 'all', label: 'All' },
  // Unpaid matches what Home counts: every active booking that is not fully paid.
  { key: 'unpaid', label: 'Unpaid' },
  { key: 'paid', label: 'Paid' },
  { key: 'partially-paid', label: 'Partially paid' },
  { key: 'pending', label: 'Pending' },
];

const sortOptions = [
  { key: 'date-desc', label: 'Newest first' },
  { key: 'date-asc', label: 'Oldest first' },
  { key: 'revenue-desc', label: 'Highest revenue' },
  { key: 'revenue-asc', label: 'Lowest revenue' },
  { key: 'pax-desc', label: 'Most passengers' },
  { key: 'pax-asc', label: 'Fewest passengers' },
  { key: 'client-asc', label: 'Client A to Z' },
  { key: 'client-desc', label: 'Client Z to A' },
];

const firstDirection: Record<string, SortDirection> = { date: 'desc', client: 'asc', pax: 'desc', revenue: 'desc' };

const filters = useQueryState(
  { q: '', payment: 'all', period: 'all', from: '', to: '', voided: '', sort: 'date-desc' },
  {
    payment: (value) => paymentOptions.some((option) => option.key === value),
    period: isPeriodKey,
    from: isIsoDate,
    to: isIsoDate,
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

const showVoided = computed(() => filters.voided === '1');
const stacked = useMediaQuery('(max-width: 860px)');

const range = computed(() => resolvePeriod(periodKey.value, businessToday.value, filters.from, filters.to));

const searched = computed(() => {
  const term = filters.q.trim().toLowerCase();
  return bookings.value.filter((b) => {
    if (!inRange(b.ds, range.value)) return false;
    if (!term) return true;
    return [b.client, b.package, b.destination, b.id].some((field) => (field ?? '').toLowerCase().includes(term));
  });
});

const voidedInScope = computed(() => searched.value.filter((b) => b.voided).length);
const pool = computed(() => (showVoided.value ? searched.value : searched.value.filter((b) => !b.voided)));

function isUnpaid(b: Booking): boolean {
  return !b.voided && b.paymentStatus !== 'Paid';
}

function paymentMatches(b: Booking, key: string): boolean {
  switch (key) {
    case 'all':
      return true;
    case 'unpaid':
      return isUnpaid(b);
    case 'paid':
      return b.paymentStatus === 'Paid';
    case 'partially-paid':
      return b.paymentStatus === 'Partially Paid';
    case 'pending':
      return b.paymentStatus === 'Pending';
    default:
      return true;
  }
}

const paymentCounts = computed(() => {
  const counts: Record<string, number> = {};
  for (const option of paymentOptions) {
    counts[option.key] = pool.value.filter((b) => paymentMatches(b, option.key)).length;
  }
  return counts;
});

const matching = computed(() => pool.value.filter((b) => paymentMatches(b, filters.payment)));

// Revenue is gross booking revenue with voided bookings left out, the same figure Home and
// Reports use. Voided bookings can be listed, but they never count.
const active = computed(() => matching.value.filter((b) => !b.voided));
const activePassengers = computed(() => active.value.reduce((sum, b) => sum + b.y, 0));
const activeRevenue = computed(() => active.value.reduce((sum, b) => sum + b.grossRevenue, 0));
// The booked value of bookings not yet fully paid. Part-payments are not recorded, so this is
// the bookings' value, not the balance still owed.
const unpaidInView = computed(() => active.value.filter(isUnpaid));
const unpaidValue = computed(() => unpaidInView.value.reduce((sum, b) => sum + b.grossRevenue, 0));

const sorted = computed(() => {
  const { field, direction } = sortParts(filters.sort);
  const sign = direction === 'asc' ? 1 : -1;
  return [...matching.value].sort((a, b) => {
    let order = 0;
    if (field === 'client') order = compareText(a.client, b.client);
    else if (field === 'pax') order = a.y - b.y;
    else if (field === 'revenue') order = a.grossRevenue - b.grossRevenue;
    if (order === 0) order = a.ds < b.ds ? -1 : a.ds > b.ds ? 1 : a.backendId - b.backendId;
    return order * sign;
  });
});

const { visible, total, shown, loadMore, showAll } = usePaged(sorted, PAGE);

const byDate = computed(() => sortParts(filters.sort).field === 'date');

interface MonthTotals {
  count: number;
  passengers: number;
  revenue: number;
}

function totalsOf(items: Booking[]): MonthTotals {
  const counted = items.filter((b) => !b.voided);
  return {
    count: counted.length,
    passengers: counted.reduce((sum, b) => sum + b.y, 0),
    revenue: counted.reduce((sum, b) => sum + b.grossRevenue, 0),
  };
}

const sections = computed(() =>
  byDate.value
    ? monthSections(visible.value, sorted.value, (b) => b.ds, totalsOf)
    : singleSection<Booking, MonthTotals>(visible.value),
);

const isFiltered = computed(
  () => filters.q.trim() !== '' || filters.payment !== 'all' || periodKey.value !== 'all' || showVoided.value,
);

const scopeText = computed(() => {
  const parts = [range.value ? describeRange(range.value) : 'All dates'];
  const term = filters.q.trim();
  if (term) parts.push(`matching “${term}”`);
  const listedVoided = matching.value.length - active.value.length;
  if (listedVoided > 0) parts.push(`${listedVoided} voided listed, not counted`);
  return parts.join(' · ');
});

function sortBy(field: string) {
  filters.sort = nextSort(filters.sort, field, firstDirection[field] ?? 'asc');
}

function clearFilters() {
  filters.q = '';
  filters.payment = 'all';
  periodKey.value = 'all';
  filters.voided = '';
}

function dateLabel(ds: string): string {
  // Inside a month heading the year is already on screen.
  return byDate.value ? formatDay(ds) : formatDate(ds);
}

function tripLabel(b: Booking): string {
  return b.destination ? `${b.package} · ${b.destination}` : b.package;
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function isTailored(b: Booking): boolean {
  return b.packageId == null;
}

// ----- The booking panel -----

function openDetail(b: Booking) {
  detailCode.value = b.id;
  detailVoidError.value = '';
  detailActionError.value = '';
  detailOpen.value = true;
}

function editFromDetail() {
  const b = detailBooking.value;
  if (!b) return;
  detailOpen.value = false;
  openEdit(b);
  returnToDetail.value = true;
}

async function quickUpdate(action: 'paid' | 'confirm') {
  const b = detailBooking.value;
  if (!b || detailBusy.value) return;
  detailBusy.value = action;
  detailActionError.value = '';
  try {
    await api.bulkBookings([b.id], action, { [b.id]: b.revision });
    await load(false);
    historyVersion.value += 1;
    toast.success(action === 'paid' ? `Booking ${b.id} marked as paid` : `Booking ${b.id} confirmed`);
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) {
      await load(false);
      detailActionError.value = 'This booking changed while it was open. The latest details are shown; try again if it still needs the update.';
    } else {
      detailActionError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not update the booking.';
    }
  } finally {
    detailBusy.value = '';
  }
}

async function voidFromDetail(reason: string) {
  const b = detailBooking.value;
  if (!b || detailBusy.value || !reason) return;
  detailBusy.value = 'void';
  detailVoidError.value = '';
  try {
    await api.voidBooking(b.id, reason, b.revision);
    await load(false);
    historyVersion.value += 1;
    toast.success('Booking ' + b.id + ' voided');
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) await load(false);
    detailVoidError.value = e instanceof ApiError ? conflictMessage(e, 'booking') : 'Could not void the booking.';
  } finally {
    detailBusy.value = '';
  }
}

async function restoreFromDetail() {
  const b = detailBooking.value;
  if (!b || detailBusy.value) return;
  detailBusy.value = 'restore';
  detailActionError.value = '';
  try {
    await api.restoreBooking(b.id, b.revision);
    await load(false);
    historyVersion.value += 1;
    toast.success('Booking ' + b.id + ' restored');
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) await load(false);
    detailActionError.value = e instanceof ApiError ? conflictMessage(e, 'booking') : 'Could not restore the booking.';
  } finally {
    detailBusy.value = '';
  }
}

// ----- The booking form -----

function blank(): BookingForm {
  return {
    id: '',
    ds: businessToday.value,
    y: 1,
    client: '',
    package: '',
    packageId: null,
    entryType: 'Package preset',
    destination: '',
    grossRevenue: 0,
    paymentStatus: 'Pending',
    bookingStatus: 'Pending',
    staffAssigned: 'Staff User',
    source: 'Package preset',
    notes: '',
    revision: null,
    mode: 'package',
  };
}

function applyPackage() {
  if (form.mode !== 'package') return;
  const preset = packages.value.find((p) => p.id === form.packageId);
  if (!preset) return;
  form.package = preset.packageName;
  form.destination = preset.destination;
  form.grossRevenue = Number(form.y || 1) * preset.basePrice;
  form.entryType = 'Package preset';
  form.source = 'Package preset';
}

function setMode(next: BookingMode) {
  if (form.mode === next) return;
  form.mode = next;
  if (next === 'custom') {
    form.packageId = null;
    form.entryType = 'Custom quotation';
    form.source = 'Manual quotation';
  } else {
    form.entryType = 'Package preset';
    form.source = 'Package preset';
    if (!form.packageId) form.packageId = packages.value[0]?.id ?? null;
    applyPackage();
  }
}

function openForm() {
  if (!businessToday.value) {
    formError.value = businessDateError;
    toast.error(businessDateError);
    return;
  }
  Object.assign(form, blank());
  form.id = 'BKG-' + (2400 + bookings.value.length + 1);
  form.packageId = packages.value[0]?.id ?? null;
  applyPackage();
  editing.value = false;
  originalCode.value = '';
  drawerTitle.value = 'New booking';
  formError.value = '';
  unusualWarning.value = '';
  confirmUnusual.value = false;
  pendingRecovery.value = null;
  returnToDetail.value = false;
  detailOpen.value = false;
  showForm.value = true;
}

function closeForm() {
  showForm.value = false;
  if (returnToDetail.value && detailBooking.value) detailOpen.value = true;
  returnToDetail.value = false;
}

function loadBookingIntoForm(b: Booking) {
  Object.assign(form, {
    id: b.id,
    ds: b.ds,
    y: b.y,
    client: b.client,
    package: b.package,
    packageId: b.packageId,
    entryType: b.entryType,
    destination: b.destination,
    grossRevenue: b.grossRevenue,
    paymentStatus: b.paymentStatus,
    bookingStatus: b.bookingStatus,
    staffAssigned: b.staffAssigned,
    source: b.source,
    notes: b.notes ?? '',
    revision: b.revision,
    mode: b.packageId == null ? 'custom' : 'package',
  } satisfies BookingForm);
}

function openEdit(b: Booking) {
  loadBookingIntoForm(b);
  editing.value = true;
  originalCode.value = b.id;
  drawerTitle.value = 'Edit booking';
  formError.value = '';
  unusualWarning.value = '';
  confirmUnusual.value = false;
  pendingRecovery.value = null;
  showForm.value = true;
}

function validate(): string | null {
  if (!form.client) return 'Please enter the client or agency partner.';
  if (!form.destination) return 'Please enter a destination.';
  if (!form.package) return form.mode === 'custom' ? 'Please name the trip or quotation.' : 'Please choose a package.';
  if (!(form.y >= 1)) return 'There must be at least one passenger.';
  if (!(form.grossRevenue >= 0)) return 'Price cannot be negative.';
  return null;
}

async function load(showErrorToast = true): Promise<boolean> {
  try {
    const config = await api.appConfig();
    businessToday.value = config.today;
    const payload = await api.bookings();
    bookings.value = payload.bookings;
    packages.value = payload.packages;
    return true;
  } catch {
    if (showErrorToast) {
      if (!businessToday.value) toast.error(businessDateError);
      toast.error('We could not load the bookings. Please refresh and try again.');
    }
    return false;
  } finally {
    loading.value = false;
  }
}

function openImport() {
  importFile.value = null;
  importPreviewData.value = null;
  importError.value = '';
  showImport.value = true;
}

async function pickImportFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  input.value = '';
  if (!file) return;
  importFile.value = file;
  importPreviewData.value = null;
  importError.value = '';
  importBusy.value = true;
  try {
    importPreviewData.value = await api.importBookingsPreview(file);
  } catch (e) {
    importError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not read that file.';
    importFile.value = null;
  } finally {
    importBusy.value = false;
  }
}

async function commitImport() {
  const file = importFile.value;
  if (!file || importBusy.value) return;
  importBusy.value = true;
  importError.value = '';
  try {
    const result = await api.importBookingsCommit(file);
    await load();
    showImport.value = false;
    toast.success(result.imported + ' booking' + (result.imported === 1 ? '' : 's') + ' imported');
  } catch (e) {
    importError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not import the file.';
  } finally {
    importBusy.value = false;
  }
}

function conflictMessage(error: ApiError, noun: string): string {
  if (error.status !== 409) return Object.values(error.fields)[0] ?? error.message;
  if (error.code === 'duplicate_code') return error.message;
  if (error.code === 'stale_revision' || error.code === 'write_conflict') {
    return `This ${noun} changed while you were working. Review the refreshed record before saving again.`;
  }
  return Object.values(error.fields)[0] ?? error.message;
}

function sameBooking(saved: Booking, submitted: BookingInput): boolean {
  return saved.id === submitted.id
    && saved.ds === submitted.ds
    && saved.y === submitted.y
    && saved.client === submitted.client
    && saved.package === submitted.package
    && saved.packageId === submitted.packageId
    && saved.entryType === submitted.entryType
    && saved.destination === submitted.destination
    && saved.grossRevenue === submitted.grossRevenue
    && saved.paymentStatus === submitted.paymentStatus
    && saved.bookingStatus === submitted.bookingStatus
    && (saved.staffAssigned ?? '') === (submitted.staffAssigned ?? '')
    && saved.source === submitted.source
    && (saved.notes ?? '') === (submitted.notes ?? '');
}

function applyCreatedBookingRecovery(payload: BookingInput): boolean {
  const saved = bookings.value.find((b) => b.id === payload.id);
  if (!saved) return false;

  unusualWarning.value = '';
  confirmUnusual.value = false;
  if (sameBooking(saved, payload)) {
    showForm.value = false;
    toast.success('Booking ' + saved.id + ' saved');
    return true;
  }

  loadBookingIntoForm(saved);
  editing.value = true;
  originalCode.value = saved.id;
  drawerTitle.value = 'Edit booking';
  const message = 'A booking with this ID is already saved with different details. Review the saved booking before changing it.';
  formError.value = message;
  return true;
}

async function recoverCreatedBooking(payload: BookingInput): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'create', payload };
    formError.value = createRecoveryError;
    return true;
  }
  return applyCreatedBookingRecovery(payload);
}

function applyBookingConflictRefresh(message: string): boolean {
  const saved = bookings.value.find((b) => b.id === originalCode.value);
  if (!saved) return false;

  loadBookingIntoForm(saved);
  unusualWarning.value = '';
  confirmUnusual.value = false;
  formError.value = message;
  return true;
}

async function refreshOpenBookingAfterConflict(message: string): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'stale-edit', message };
    formError.value = staleRecoveryError;
    return true;
  }
  return applyBookingConflictRefresh(message);
}

async function continuePendingRecovery(): Promise<boolean> {
  const pending = pendingRecovery.value;
  if (!pending) return false;

  if (!await load(false)) {
    formError.value = pending.kind === 'create' ? createRecoveryError : staleRecoveryError;
    return true;
  }

  pendingRecovery.value = null;
  if (pending.kind === 'create') {
    if (applyCreatedBookingRecovery(pending.payload)) return true;
    formError.value = 'The booking was not found after reloading. Review the draft, then save again if it still needs to be created.';
    return true;
  }

  if (applyBookingConflictRefresh(pending.message)) return true;
  formError.value = 'The booking was not found after reloading. Refresh the page before making another change.';
  return true;
}

async function save() {
  if (saving.value) return;
  if (pendingRecovery.value) {
    formError.value = '';
    saving.value = true;
    try {
      await continuePendingRecovery();
    } finally {
      saving.value = false;
    }
    return;
  }
  const problem = validate();
  if (problem) {
    formError.value = problem;
    return;
  }
  formError.value = '';
  saving.value = true;
  const payload: BookingInput = {
    id: form.id,
    ds: form.ds,
    y: form.y,
    client: form.client,
    package: form.package,
    packageId: form.packageId,
    entryType: form.entryType,
    destination: form.destination,
    grossRevenue: form.grossRevenue,
    paymentStatus: form.paymentStatus,
    bookingStatus: form.bookingStatus,
    staffAssigned: form.staffAssigned,
    source: form.source,
    notes: form.notes,
    revision: form.revision,
    confirmUnusual: confirmUnusual.value,
  };
  try {
    if (editing.value) await api.updateBooking(originalCode.value, payload);
    else await api.createBooking(payload);
    await load();
    pendingRecovery.value = null;
    confirmUnusual.value = false;
    unusualWarning.value = '';
    historyVersion.value += 1;
    toast.success('Booking ' + payload.id + ' saved');
    // An edit goes back to the booking it came from, now showing the change.
    if (editing.value) detailCode.value = payload.id;
    showForm.value = false;
    if (returnToDetail.value) detailOpen.value = true;
    returnToDetail.value = false;
  } catch (e) {
    if (e instanceof ApiError && e.status === 409 && e.code === 'unusual_revenue') {
      unusualWarning.value = e.message;
      confirmUnusual.value = true;
      saving.value = false;
      return;
    }
    if (!editing.value && (!(e instanceof ApiError) || e.status >= 500 || e.status === 409)) {
      if (await recoverCreatedBooking(payload)) return;
    }
    if (e instanceof ApiError && e.status === 409) {
      const message = conflictMessage(e, 'booking');
      if (editing.value && (e.code === 'stale_revision' || e.code === 'write_conflict')
        && await refreshOpenBookingAfterConflict(message)) return;
      await load();
    }
    const message = e instanceof ApiError ? conflictMessage(e, 'booking') : 'Could not save the booking.';
    formError.value = message;
  } finally {
    saving.value = false;
  }
}

onMounted(async () => {
  await load();
  // Home's "Record a booking" arrives with ?new=1; open the form once the data it needs is in.
  if (route.query.new === '1') {
    openForm();
    void router.replace({ query: { ...route.query, new: undefined } });
  }
});
</script>

<style scoped>
.booking-mode {
  border: none;
  padding: 0;
  margin: 0 0 var(--space-5);
  min-width: 0;
}

.booking-mode legend {
  padding: 0;
  margin-bottom: 8px;
}

.booking-mode-options {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px;
  background: var(--color-surface-sunken);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.booking-mode-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 500;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.booking-mode-option:hover {
  color: var(--color-text-primary);
}

.booking-mode-option input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  margin: 0;
}

.booking-mode-option.selected {
  background: var(--color-surface);
  color: var(--color-primary);
  box-shadow: var(--shadow-md);
}

.booking-mode-option:focus-within {
  outline: 2px solid var(--color-ring);
  outline-offset: 1px;
}

.trip-name {
  display: block;
  color: var(--color-text-primary);
}

.kind-tag {
  display: inline-block;
  margin-left: 6px;
  padding: 0 7px;
  border-radius: var(--radius-pill);
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
  font-size: var(--text-xs);
  font-weight: 600;
}
</style>
