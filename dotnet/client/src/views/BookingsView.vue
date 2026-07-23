<template>
  <AppShell title="Bookings" description="Booking and transaction records.">
    <template #actions>
      <a class="secondary-button" href="/api/export/bookings.csv">Export CSV</a>
      <button class="secondary-button" type="button" @click="openImport">Import from file</button>
      <button class="primary-button" type="button" @click="openForm">Add booking</button>
    </template>

    <section class="dss-page">
      <section class="stat-band" aria-label="Booking totals">
        <div class="stat-cell">
          <span class="stat-label">Bookings</span>
          <strong class="stat-value">{{ bookings.length }}</strong>
          <span class="stat-note">Saved bookings</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Passengers</span>
          <strong class="stat-value">{{ totalPassengers }}</strong>
          <span class="stat-note">Across all bookings</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Booked revenue</span>
          <strong class="stat-value">{{ peso(totalRevenue) }}</strong>
          <span class="stat-note">Total revenue</span>
        </div>
      </section>

      <Drawer :open="showForm" :title="drawerTitle" @close="showForm = false">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>

        <p v-if="unusualWarning" class="drawer-form-check" role="alert">{{ unusualWarning }}</p>

        <div v-if="askVoid" class="void-panel" role="group" aria-label="Void this booking">
          <p class="void-lead">Voiding keeps the record and its history, but takes it out of every total and out of the forecast. Any seats it holds go back to the package.</p>
          <label class="account-field">
            <span>Why is it being voided?</span>
            <input v-model.trim="voidReason" maxlength="160" placeholder="Duplicate entry, client cancelled, recorded in error" />
          </label>
          <p v-if="voidError" class="drawer-form-error" role="alert">{{ voidError }}</p>
          <div class="void-actions">
            <button class="secondary-button" type="button" :disabled="voiding" @click="askVoid = false">Keep it</button>
            <button class="danger-button" type="button" :disabled="voiding || !voidReason" @click="confirmVoid">
              {{ voiding ? 'Voiding…' : 'Void booking' }}
            </button>
          </div>
        </div>

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
          <button
            v-if="editing && !editingVoided"
            class="table-link danger-link"
            type="button"
            :disabled="saving"
            @click="askVoid = true"
          >Void this booking</button>
          <span class="footer-spacer"></span>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
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

      <div v-if="loading" class="content-panel">
        <div class="panel-header">
          <div class="panel-title-group">
            <h2>All bookings</h2>
          </div>
        </div>
        <Skeleton :rows="6" />
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

      <div v-else class="content-panel bookings-panel">
        <div class="panel-header bookings-toolbar-header">
          <div class="panel-title-group">
            <h2>All bookings</h2>
            <span class="panel-meta">{{ visibleBookings.length }} of {{ bookings.length }} shown</span>
          </div>
          <SearchField v-model="query" placeholder="Search client, package, or ID" />
        </div>

        <div class="filter-chips" role="group" aria-label="Filter by payment">
          <button
            v-for="option in paymentOptions"
            :key="option"
            type="button"
            class="filter-chip"
            :class="{ active: paymentFilter === option }"
            :aria-pressed="paymentFilter === option"
            @click="paymentFilter = option"
          >
            <span>{{ option }}</span>
            <span class="chip-count">{{ paymentCounts[option] ?? 0 }}</span>
          </button>
        </div>

        <template v-if="visibleBookings.length">
          <div class="dss-table-frame">
            <table class="dss-table">
              <thead>
                <tr>
                  <th>Booking ID</th>
                  <th>Client / Partner</th>
                  <th>Package</th>
                  <th class="num">Passengers</th>
                  <th class="num">Revenue</th>
                  <th>Payment</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="b in visibleBookings" :key="b.id" :class="{ 'row-voided': b.voided }">
                  <td>
                    <strong>{{ b.id }}</strong>
                    <span v-if="b.voided" class="row-subtext voided-tag" :title="b.voidReason ?? ''">Voided</span>
                  </td>
                  <td>{{ b.client }}</td>
                  <td>
                    <strong>{{ b.package }}</strong>
                    <span class="row-subtext">
                      {{ b.destination }}
                      <span class="kind-tag" :class="{ 'kind-tailored': isTailored(b) }">
                        {{ isTailored(b) ? 'Tailored' : 'Package' }}
                      </span>
                    </span>
                  </td>
                  <td class="num">{{ b.y }}</td>
                  <td class="num"><strong>{{ peso(b.grossRevenue) }}</strong></td>
                  <td><span class="record-badge" :class="badge(b.paymentStatus)">{{ b.paymentStatus }}</span></td>
                  <td><span class="record-badge" :class="badge(b.bookingStatus)">{{ b.bookingStatus }}</span></td>
                  <td class="num">
                    <button v-if="b.voided" class="table-link" type="button" @click="restore(b)">Restore</button>
                    <button v-else class="table-link" type="button" @click="openEdit(b)">Edit</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            noun="bookings"
            @more="loadMore"
            @all="showAll"
          />
        </template>

        <div v-else class="filter-empty">
          <p>No bookings match your search.</p>
          <button class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </div>
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
import ListFooter from '../components/ListFooter.vue';
import { usePaged } from '../composables/usePaged';
import { useToast } from '../composables/useToast';
import { api, ApiError, type Booking, type BookingInput, type ImportPreview, type PackageOption } from '../api';
import { count, peso } from '../format';
import MoneyField from '../components/MoneyField.vue';

type BookingMode = 'package' | 'custom';
type BookingForm = BookingInput & { mode: BookingMode };

const toast = useToast();

const bookings = ref<Booking[]>([]);
const packages = ref<PackageOption[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');
const drawerTitle = ref('New booking');

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

const askVoid = ref(false);
const voiding = ref(false);
const voidReason = ref('');
const voidError = ref('');
const editingVoided = computed(
  () => bookings.value.find((b) => b.id === originalCode.value)?.voided ?? false,
);

const query = ref('');
const paymentOptions = ['All', 'Paid', 'Partially Paid', 'Pending'];
const paymentFilter = ref('All');

const form = reactive<BookingForm>(blank());

const totalPassengers = computed(() => bookings.value.reduce((sum, b) => sum + b.y, 0));
const totalRevenue = computed(() => bookings.value.reduce((sum, b) => sum + b.grossRevenue, 0));

const paymentCounts = computed(() => {
  const counts: Record<string, number> = { All: bookings.value.length };
  for (const b of bookings.value) counts[b.paymentStatus] = (counts[b.paymentStatus] ?? 0) + 1;
  return counts;
});

const matching = computed(() => {
  const term = query.value.trim().toLowerCase();
  return bookings.value.filter((b) => {
    if (paymentFilter.value !== 'All' && b.paymentStatus !== paymentFilter.value) return false;
    if (!term) return true;
    return [b.client, b.package, b.destination, b.id].some((field) => (field ?? '').toLowerCase().includes(term));
  });
});

const { visible: visibleBookings, total, shown, loadMore, showAll } = usePaged(matching);

function clearFilters() {
  query.value = '';
  paymentFilter.value = 'All';
}


function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function isTailored(b: Booking): boolean {
  return b.packageId == null;
}

function blank(): BookingForm {
  return {
    id: '',
    ds: new Date().toISOString().slice(0, 10),
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
  askVoid.value = false;
  voidReason.value = '';
  voidError.value = '';
  showForm.value = true;
}

function openEdit(b: Booking) {
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
    mode: b.packageId == null ? 'custom' : 'package',
  } satisfies BookingForm);
  editing.value = true;
  originalCode.value = b.id;
  drawerTitle.value = 'Edit booking';
  formError.value = '';
  unusualWarning.value = '';
  confirmUnusual.value = false;
  askVoid.value = false;
  voidReason.value = '';
  voidError.value = '';
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

async function load() {
  try {
    const payload = await api.bookings();
    bookings.value = payload.bookings;
    packages.value = payload.packages;
  } catch {
    toast.error('We could not load the bookings. Please refresh and try again.');
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

async function confirmVoid() {
  if (voiding.value || !voidReason.value) return;
  voiding.value = true;
  voidError.value = '';
  try {
    await api.voidBooking(originalCode.value, voidReason.value);
    await load();
    askVoid.value = false;
    showForm.value = false;
    toast.success('Booking ' + originalCode.value + ' voided');
  } catch (e) {
    voidError.value = e instanceof ApiError
      ? Object.values(e.fields)[0] ?? e.message
      : 'Could not void the booking.';
  } finally {
    voiding.value = false;
  }
}

async function restore(b: Booking) {
  try {
    await api.restoreBooking(b.id);
    await load();
    toast.success('Booking ' + b.id + ' restored');
  } catch (e) {
    const message = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not restore the booking.';
    toast.error(message);
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
    confirmUnusual: confirmUnusual.value,
  };
  try {
    if (editing.value) await api.updateBooking(originalCode.value, payload);
    else await api.createBooking(payload);
    await load();
    showForm.value = false;
    confirmUnusual.value = false;
    unusualWarning.value = '';
    toast.success('Booking ' + payload.id + ' saved');
  } catch (e) {
    // 409 is the server asking whether an unusually large figure was meant, not a refusal.
    if (e instanceof ApiError && e.status === 409) {
      unusualWarning.value = e.message;
      confirmUnusual.value = true;
      saving.value = false;
      return;
    }
    const message = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the booking.';
    formError.value = message;
    toast.error(message);
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<style scoped>
.drawer-form-check {
  margin-bottom: var(--space-4);
  padding: 10px 12px;
  border: 1px solid var(--tone-warning-border);
  border-radius: var(--radius-md);
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
  font-size: 13px;
  line-height: 1.5;
}

.drawer-form-error {
  margin-bottom: var(--space-4);
  color: var(--color-danger-ink);
  font-size: 13px;
}

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
  font-size: 13px;
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

.bookings-toolbar-header {
  align-items: center;
  flex-wrap: wrap;
  row-gap: var(--space-3);
}

.bookings-toolbar-header .search-field {
  flex: 1 1 240px;
  margin-left: auto;
}

.filter-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-bottom: var(--space-4);
}

.filter-chip {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border: 1px solid var(--color-border-strong);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  border-radius: var(--radius-pill);
  padding: 5px 13px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast);
}

.filter-chip:hover {
  border-color: var(--color-text-faint);
  background: var(--color-surface-sunken);
}

.filter-chip.active {
  background: var(--color-primary-soft);
  border-color: var(--color-primary);
  color: var(--color-primary-active);
  font-weight: 600;
}

.chip-count {
  font-variant-numeric: lining-nums tabular-nums;
  font-size: 12px;
  color: var(--color-text-muted);
}

.filter-chip.active .chip-count {
  color: var(--color-primary);
}

.dss-table-frame {
  overflow-x: auto;
}

.filter-empty {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  padding: var(--space-8) var(--space-1);
  color: var(--color-text-muted);
  font-size: 13.5px;
}

.kind-tag {
  display: inline-block;
  margin-left: 6px;
  padding: 1px 8px;
  border-radius: var(--radius-pill);
  background: var(--tone-neutral-surface);
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 600;
}

.kind-tag.kind-tailored {
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
}

.row-voided td {
  color: var(--color-text-faint);
}

.row-voided td strong,
.row-voided .record-badge {
  color: var(--color-text-muted);
}

.voided-tag {
  color: var(--color-danger-ink);
  font-weight: 600;
}

.void-panel {
  margin-bottom: var(--space-4);
  padding: var(--space-4);
  border: 1px solid var(--tone-danger-border);
  border-radius: var(--radius-md);
  background: var(--tone-danger-surface);
}

.void-lead {
  margin: 0 0 var(--space-3);
  color: var(--tone-danger-ink);
  font-size: 13px;
  line-height: 1.5;
}

.void-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  margin-top: var(--space-3);
}

.footer-spacer {
  flex: 1;
}

.danger-link {
  color: var(--color-danger-ink);
}

.import-hint {
  margin: 0 0 var(--space-4);
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.5;
}

.import-pick {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

.import-filename {
  color: var(--color-text-secondary);
  font-size: 13px;
  word-break: break-all;
}

.import-summary {
  display: grid;
  gap: 8px;
  margin: 0 0 var(--space-4);
  padding: var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface-sunken);
}

.import-summary > div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
}

.import-summary dt {
  color: var(--color-text-muted);
  font-size: 12.5px;
}

.import-summary dd {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
  text-align: right;
}

.import-notes {
  margin: 0 0 var(--space-4);
  padding: 10px 12px;
  border-radius: var(--radius-md);
  font-size: 12.5px;
  line-height: 1.5;
}

.import-notes p {
  margin: 0;
}

.import-notes-title {
  margin: 0 0 2px;
  font-weight: 700;
}

.import-warnings {
  border: 1px solid var(--tone-warning-border);
  background: var(--tone-warning-surface);
  color: var(--tone-warning-ink);
}

.import-problems {
  border: 1px solid var(--tone-danger-border);
  background: var(--tone-danger-surface);
  color: var(--tone-danger-ink);
}
</style>
