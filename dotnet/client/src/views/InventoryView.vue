<template>
  <AppShell title="Packages">
    <template #actions>
      <ActionMenu :items="menuItems" />
      <button class="primary-button" type="button" @click="openForm">
        <UiIcon name="plus" />
        Add package
      </button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="editingCode ? 'Edit package' : 'New package'" @close="onDrawerClose">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <div class="form-grid">
          <label class="account-field field-wide">
            <span>Package name</span>
            <input v-model.trim="form.packageName" maxlength="120" />
          </label>
          <label class="account-field">
            <span>Package code</span>
            <input v-model.trim="form.id" maxlength="40" />
          </label>
          <label class="account-field">
            <span>Status</span>
            <select v-model="form.status">
              <option>Normal</option>
              <option>Low</option>
              <option>Critical</option>
            </select>
          </label>
          <label class="account-field field-wide">
            <span>Destination</span>
            <input v-model.trim="form.destination" maxlength="120" />
          </label>
          <label class="account-field field-wide">
            <span>Duration</span>
            <input v-model.trim="form.duration" maxlength="80" />
          </label>
          <label class="account-field">
            <span>Base price</span>
            <MoneyField v-model="form.basePrice" />
          </label>
          <label class="account-field">
            <span>Available slots</span>
            <input v-model.number="form.availableSlots" type="number" min="0" />
          </label>
          <label class="account-field">
            <span>Sold slots</span>
            <input v-model.number="form.soldCount" type="number" min="0" />
          </label>
          <label class="account-field">
            <span>Reserved slots</span>
            <input v-model.number="form.reservedCount" type="number" min="0" />
          </label>
          <label class="account-field field-wide">
            <span>Inclusions</span>
            <input v-model.trim="form.inclusions" maxlength="420" />
          </label>

          <div class="account-field field-wide">
            <span>Package photo</span>

            <p v-if="!editingCode" class="image-hint">
              Save the package first, then reopen it to add a photo.
            </p>

            <template v-else>
              <div class="image-row">
                <button
                  v-if="editingImage"
                  type="button"
                  class="image-preview image-preview-zoom"
                  :aria-label="'View photo of ' + form.packageName"
                  @click="openPreviewLightbox"
                >
                  <img :src="editingImage" alt="" />
                </button>

                <div class="image-actions">
                  <input
                    ref="fileInput"
                    class="visually-hidden"
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    @change="pickImage"
                  />
                  <button
                    class="secondary-button"
                    type="button"
                    :disabled="imageBusy"
                    @click="fileInput?.click()"
                  >
                    <UiIcon name="image" />
                    {{ imageBusy ? 'Uploading…' : editingImage ? 'Replace photo' : 'Add a photo' }}
                  </button>
                  <button
                    v-if="editingImage"
                    class="table-link"
                    type="button"
                    :disabled="imageBusy"
                    @click="dropImage"
                  >
                    Remove
                  </button>
                </div>
              </div>

              <p v-if="imageError" class="image-error" role="alert">{{ imageError }}</p>
              <p v-else class="image-hint">JPEG, PNG, or WebP. Up to 4 MB.</p>
            </template>
          </div>
        </div>

        <RecordHistory
          v-if="editingCode && canSeeHistory"
          class="form-history"
          entity-type="TravelPackage"
          :code="editingCode"
          :version="historyVersion"
        />

        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : editingCode ? 'Save changes' : 'Save package' }}
          </button>
        </template>
      </Drawer>

      <Drawer :open="showImport" title="Import packages from a file" @close="showImport = false">
        <p class="import-hint">
          Export the sheet as a CSV file with the columns package, destination, and price.
          Code, duration, inclusions, slots, and status are optional, and extra columns
          are ignored.
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
              <dt>Already in the catalog</dt>
              <dd>{{ count(importPreviewData.duplicates) }}</dd>
            </div>
            <div v-if="importPreviewData.skipped">
              <dt>Rows that could not be read</dt>
              <dd>{{ count(importPreviewData.skipped) }}</dd>
            </div>
            <div>
              <dt>Destinations</dt>
              <dd>{{ count(importPreviewData.destinations) }}</dd>
            </div>
            <div>
              <dt>Total open slots</dt>
              <dd>{{ count(importPreviewData.totalSlots) }}</dd>
            </div>
          </dl>

          <p v-if="importPreviewData.duplicateNames.length" class="import-hint">
            Already in the catalog: {{ importPreviewData.duplicateNames.slice(0, 5).join(', ') }}<template v-if="importPreviewData.duplicateNames.length > 5"> and {{ importPreviewData.duplicateNames.length - 5 }} more</template>.
          </p>

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
            Nothing is saved until you press import. Imported packages start with the slots
            the file gives them and can be edited afterwards like any other package.
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
              ? 'Import ' + count(importable) + ' package' + (importable === 1 ? '' : 's')
              : 'Nothing to import' }}
          </button>
        </template>
      </Drawer>

      <OutlookStrip
        v-if="!loading && packages.length"
        :low-count="lowPackages.length"
        :low-active="lowOnly"
        :open-slots="allOpenSlots"
        @show-low="filters.stock = 'low'"
      />

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="8" />
      </div>

      <div v-else-if="loadError" class="content-panel load-error" role="alert">
        <p class="load-error-title">The packages could not be loaded</p>
        <p class="load-error-text">Check the connection, then try again. Nothing has been changed.</p>
        <button class="secondary-button" type="button" @click="retryLoad">Try again</button>
      </div>
      <EmptyState
        v-else-if="packages.length === 0"
        title="No packages yet"
        message="Add your first travel package to start tracking available slots and sales."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openForm">Add package</button>
        </template>
      </EmptyState>

      <section v-else class="content-panel records-panel" aria-label="Packages">
        <div class="records-tools">
          <SearchField v-model="filters.q" placeholder="Search name, destination or code" />
          <button
            type="button"
            class="filter-chip"
            :aria-pressed="lowOnly"
            @click="filters.stock = lowOnly ? '' : 'low'"
          >
            Low on slots
            <span class="chip-count">{{ count(lowPackages.length) }}</span>
          </button>
          <label v-if="stacked" class="records-sort">
            <span class="visually-hidden">Sort packages</span>
            <select v-model="filters.sort">
              <option v-for="option in sortOptions" :key="option.key" :value="option.key">{{ option.label }}</option>
            </select>
          </label>
        </div>

        <p class="records-summary" aria-live="polite">
          <span class="summary-figure"><strong>{{ count(filtered.length) }}</strong> {{ filtered.length === 1 ? 'package' : 'packages' }}</span>
          <span class="summary-figure"><strong>{{ count(openSlots) }}</strong> open slots</span>
          <span class="summary-figure"><strong>{{ count(soldSlots) }}</strong> sold</span>
          <span v-if="scopeText" class="summary-scope">{{ scopeText }}</span>
          <button v-if="isFiltered" class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </p>

        <template v-if="filtered.length > 0">
          <ul v-if="stacked" class="record-list">
            <li v-for="p in visiblePackages" :key="p.id">
              <button class="record-item" type="button" @click="openEdit(p)">
                <span class="record-item-top">
                  <span class="record-item-title">{{ p.packageName }}</span>
                  <span class="record-item-figure">{{ peso(p.basePrice) }}</span>
                </span>
                <span class="record-item-sub">{{ placeLabel(p) }}</span>
                <span class="slot-line">
                  <span class="slot-meter" aria-hidden="true">
                    <span class="slot-sold" :style="{ width: share(p, p.soldCount) }"></span>
                    <span class="slot-reserved" :style="{ width: share(p, p.reservedCount) }"></span>
                  </span>
                </span>
                <span class="record-item-meta">
                  <span>
                    <span class="slots-left" :class="tone(p)">{{ slotsLeft(p) }}</span>
                    · {{ count(p.soldCount) }} sold · {{ p.id }}
                  </span>
                  <span class="record-badge" :class="badge(p.status)">{{ stateLabel(p.status) }}</span>
                </span>
              </button>
            </li>
          </ul>

          <div v-else class="dss-table-frame">
            <table class="dss-table records-table packages-table">
              <thead>
                <tr>
                  <SortHeader label="Package" field="name" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Destination</th>
                  <SortHeader label="Price" field="price" align="right" :sort="filters.sort" @sort="sortBy" />
                  <SortHeader label="Slots" field="slots" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="p in visiblePackages" :key="p.id" class="record-row" @click="openEdit(p)">
                  <td>
                    <span class="package-cell">
                      <button
                        v-if="p.imageUrl"
                        type="button"
                        class="package-thumb"
                        :aria-label="'View photo of ' + p.packageName"
                        @click.stop="openLightbox(p)"
                      >
                        <img :src="p.imageUrl" alt="" loading="lazy" decoding="async" />
                      </button>
                      <span v-else-if="anyPhotos" class="package-thumb-space" aria-hidden="true"></span>
                      <span class="package-names">
                        <button class="row-open" type="button" @click.stop="openEdit(p)">{{ p.packageName }}</button>
                        <span class="row-subtext">{{ p.id }}</span>
                      </span>
                    </span>
                  </td>
                  <td>
                    <span class="place-name">{{ p.destination }}</span>
                    <span v-if="p.duration" class="row-subtext">{{ p.duration }}</span>
                  </td>
                  <td class="num"><strong>{{ peso(p.basePrice) }}</strong></td>
                  <td class="slots-cell">
                    <span class="slot-meter" aria-hidden="true">
                      <span class="slot-sold" :style="{ width: share(p, p.soldCount) }"></span>
                      <span class="slot-reserved" :style="{ width: share(p, p.reservedCount) }"></span>
                    </span>
                    <span class="slot-text">
                      <span class="slots-left" :class="tone(p)">{{ slotsLeft(p) }}</span>
                      <span class="slot-detail">{{ slotDetail(p) }}</span>
                    </span>
                  </td>
                  <td><span class="record-badge" :class="badge(p.status)">{{ stateLabel(p.status) }}</span></td>
                </tr>
              </tbody>
            </table>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            :page-size="PAGE"
            noun="packages"
            @more="loadMore"
            @all="showAll"
          />
        </template>

        <div v-else class="records-empty">
          <strong>No packages match</strong>
          <span>{{ emptyMessage }}</span>
          <button class="table-link" type="button" @click="clearFilters">Show all packages</button>
        </div>
      </section>

      <Lightbox :url="lightboxUrl" :caption="lightboxName" @close="lightboxUrl = null" />
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import ActionMenu, { type ActionMenuItem } from '../components/ActionMenu.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import ListFooter from '../components/ListFooter.vue';
import Lightbox from '../components/Lightbox.vue';
import RecordHistory from '../components/RecordHistory.vue';
import SortHeader from '../components/SortHeader.vue';
import UiIcon from '../components/UiIcon.vue';
import MoneyField from '../components/MoneyField.vue';
import OutlookStrip from '../components/forecast/OutlookStrip.vue';
import { usePaged } from '../composables/usePaged';
import { useToast } from '../composables/useToast';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useQueryState } from '../composables/useQueryState';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { api, ApiError, type PackageImportPreview, type PackageRow, type PackageInput } from '../api';
import { count, peso, stateLabel } from '../format';
import { compareText, nextSort, sortParts, type SortDirection } from '../listing';

type PackageRecovery =
  | { kind: 'create'; payload: PackageInput }
  | { kind: 'stale-edit'; message: string };

const PAGE = 50;

const toast = useToast();
const { state: auth } = useAuth();

const packages = ref<PackageRow[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editingCode = ref<string | null>(null);
const pendingRecovery = ref<PackageRecovery | null>(null);
const createRecoveryError = 'Could not confirm whether this package was saved. Check your connection, then try again before changing this form.';
const staleRecoveryError = 'Could not reload the latest package after a conflict. Check your connection, then try again before saving.';
const historyVersion = ref(0);
// The change history comes from the activity log, which Owner and Admin can read.
const canSeeHistory = computed(() => canAccess(auth.user?.role, '/reports'));

const form = reactive<PackageInput>(blank());

const lightboxUrl = ref<string | null>(null);
const lightboxName = ref('');

const showImport = ref(false);
const importFileInput = ref<HTMLInputElement | null>(null);
const importFile = ref<File | null>(null);
const importPreviewData = ref<PackageImportPreview | null>(null);
const importBusy = ref(false);
const importError = ref('');

const importable = computed(() => {
  const p = importPreviewData.value;
  return p ? Math.max(0, p.valid - p.duplicates) : 0;
});

const menuItems: ActionMenuItem[] = [
  { label: 'Export packages', hint: 'The whole catalog as a CSV file', icon: 'download', href: '/api/export/packages.csv' },
  { label: 'Import from a file', hint: 'Add packages from a CSV sheet', icon: 'upload', run: openImport },
];

// ----- Filters, kept in the address -----

const sortOptions = [
  { key: 'name-asc', label: 'Name A to Z' },
  { key: 'name-desc', label: 'Name Z to A' },
  { key: 'slots-asc', label: 'Fewest slots left' },
  { key: 'slots-desc', label: 'Most slots left' },
  { key: 'price-desc', label: 'Highest price' },
  { key: 'price-asc', label: 'Lowest price' },
];

const firstDirection: Record<string, SortDirection> = { name: 'asc', price: 'desc', slots: 'asc' };

const filters = useQueryState(
  { q: '', stock: '', sort: 'name-asc' },
  {
    stock: (value) => value === 'low',
    sort: (value) => sortOptions.some((option) => option.key === value),
  },
);

const lowOnly = computed(() => filters.stock === 'low');
const stacked = useMediaQuery('(max-width: 860px)');

// Same rule as the Home attention list: packages flagged Low or Critical, or, when none are
// flagged, any package with five or fewer slots.
const lowPackages = computed(() => {
  const flagged = packages.value.filter((p) => p.status === 'Low' || p.status === 'Critical');
  return flagged.length > 0 ? flagged : packages.value.filter((p) => p.availableSlots <= 5);
});

const filtered = computed(() => {
  const q = filters.q.trim().toLowerCase();
  const pool = lowOnly.value ? lowPackages.value : packages.value;
  if (!q) return pool;
  return pool.filter((p) =>
    p.packageName.toLowerCase().includes(q) ||
    p.id.toLowerCase().includes(q) ||
    (p.destination ?? '').toLowerCase().includes(q)
  );
});

const sorted = computed(() => {
  const { field, direction } = sortParts(filters.sort);
  const sign = direction === 'asc' ? 1 : -1;
  return [...filtered.value].sort((a, b) => {
    let order = 0;
    if (field === 'price') order = a.basePrice - b.basePrice;
    else if (field === 'slots') order = a.availableSlots - b.availableSlots;
    if (order === 0) order = compareText(a.packageName, b.packageName);
    return order * sign;
  });
});

const { visible: visiblePackages, total, shown, loadMore, showAll } = usePaged(sorted, PAGE);

const openSlots = computed(() => filtered.value.reduce((sum, p) => sum + p.availableSlots, 0));
const allOpenSlots = computed(() => packages.value.reduce((sum, p) => sum + p.availableSlots, 0));
const soldSlots = computed(() => filtered.value.reduce((sum, p) => sum + p.soldCount, 0));
const anyPhotos = computed(() => visiblePackages.value.some((p) => p.imageUrl));

const isFiltered = computed(() => filters.q.trim() !== '' || lowOnly.value);

const scopeText = computed(() => {
  const parts: string[] = [];
  if (lowOnly.value) parts.push('Low on slots');
  const term = filters.q.trim();
  if (term) parts.push(`matching “${term}”`);
  return parts.join(' · ');
});

const emptyMessage = computed(() => {
  const q = filters.q.trim();
  if (q && lowOnly.value) return `No package low on slots matches “${q}”.`;
  if (q) return `Nothing matches “${q}”. Try a different name, code or destination.`;
  return 'Every package has slots available.';
});

function sortBy(field: string) {
  filters.sort = nextSort(filters.sort, field, firstDirection[field] ?? 'asc');
}

function clearFilters() {
  filters.q = '';
  filters.stock = '';
}

function capacity(p: PackageRow): number {
  return p.availableSlots + p.soldCount + p.reservedCount;
}

function share(p: PackageRow, part: number): string {
  const whole = capacity(p);
  return whole > 0 ? `${Math.min(100, (part / whole) * 100)}%` : '0%';
}

function slotsLeft(p: PackageRow): string {
  if (p.availableSlots === 0) return 'Full';
  return `${count(p.availableSlots)} left`;
}

function slotDetail(p: PackageRow): string {
  const parts = [`of ${count(capacity(p))}`];
  if (p.reservedCount > 0) parts.push(`${count(p.reservedCount)} reserved`);
  return parts.join(' · ');
}

function tone(p: PackageRow): string {
  if (p.status === 'Critical' || p.availableSlots === 0) return 'tone-danger';
  if (p.status === 'Low') return 'tone-warning';
  return '';
}

function placeLabel(p: PackageRow): string {
  return p.duration ? `${p.destination} · ${p.duration}` : p.destination;
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

// ----- The package form -----

const fileInput = ref<HTMLInputElement | null>(null);
const imageBusy = ref(false);
const imageError = ref('');

const editingImage = computed(
  () => packages.value.find((p) => p.id === editingCode.value)?.imageUrl ?? null,
);

const editingPackage = computed(
  () => packages.value.find((p) => p.id === editingCode.value) ?? null,
);

function replaceRow(updated: PackageRow) {
  const index = packages.value.findIndex((p) => p.id === updated.id);
  if (index >= 0) packages.value[index] = updated;
}

function replaceRowAndAdvanceDraft(updated: PackageRow, requestRevision: number) {
  replaceRow(updated);
  if (editingCode.value === updated.id && form.revision === requestRevision) {
    form.revision = updated.revision;
  }
}

function conflictMessage(error: ApiError, noun: string): string {
  if (error.status !== 409) return Object.values(error.fields)[0] ?? error.message;
  if (error.code === 'stale_revision' || error.code === 'write_conflict') {
    return `This ${noun} changed while you were working. Review the refreshed record before saving again.`;
  }
  return Object.values(error.fields)[0] ?? error.message;
}

async function pickImage(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  const code = editingCode.value;
  const revision = form.revision;
  input.value = '';
  if (!file || !code || revision == null) return;

  imageError.value = '';
  imageBusy.value = true;
  try {
    replaceRowAndAdvanceDraft(await api.uploadPackageImage(code, file, revision), revision);
    historyVersion.value += 1;
    toast.success('Photo updated.');
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      await refreshOpenPackageAfterConflict(conflictMessage(error, 'package'));
    }
    imageError.value =
      error instanceof ApiError
        ? error.fields.image ?? conflictMessage(error, 'package')
        : 'Could not upload that photo.';
  } finally {
    imageBusy.value = false;
  }
}

async function dropImage() {
  const code = editingCode.value;
  const revision = form.revision;
  if (!code || revision == null) return;

  imageError.value = '';
  imageBusy.value = true;
  try {
    replaceRowAndAdvanceDraft(await api.removePackageImage(code, revision), revision);
    historyVersion.value += 1;
    toast.success('Photo removed.');
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      await refreshOpenPackageAfterConflict(conflictMessage(error, 'package'));
    }
    imageError.value = error instanceof ApiError ? conflictMessage(error, 'package') : 'Could not remove that photo.';
  } finally {
    imageBusy.value = false;
  }
}

function blank(): PackageInput {
  return {
    id: '',
    packageName: '',
    destination: '',
    duration: '',
    basePrice: 0,
    inclusions: '',
    availableSlots: 0,
    soldCount: 0,
    reservedCount: 0,
    status: 'Normal',
    revision: null,
  };
}

function openForm() {
  Object.assign(form, blank());
  form.id = 'PKG-' + (100 + packages.value.length + 1);
  editingCode.value = null;
  formError.value = '';
  imageError.value = '';
  pendingRecovery.value = null;
  showForm.value = true;
}

function loadPackageIntoForm(p: PackageRow) {
  Object.assign(form, {
    id: p.id,
    packageName: p.packageName,
    destination: p.destination,
    duration: p.duration,
    basePrice: p.basePrice,
    inclusions: p.inclusions,
    availableSlots: p.availableSlots,
    soldCount: p.soldCount,
    reservedCount: p.reservedCount,
    status: p.status,
    revision: p.revision,
  });
}

function openEdit(p: PackageRow) {
  loadPackageIntoForm(p);
  editingCode.value = p.id;
  formError.value = '';
  imageError.value = '';
  pendingRecovery.value = null;
  showForm.value = true;
}

function openLightbox(p: PackageRow) {
  lightboxUrl.value = p.imageUrl;
  lightboxName.value = p.packageName;
}

function openPreviewLightbox() {
  lightboxUrl.value = editingImage.value;
  lightboxName.value = form.packageName;
}

function onDrawerClose() {
  if (lightboxUrl.value) return;
  showForm.value = false;
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
    importPreviewData.value = await api.importPackagesPreview(file);
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
    const result = await api.importPackagesCommit(file);
    await load();
    showImport.value = false;
    toast.success(result.imported + ' package' + (result.imported === 1 ? '' : 's') + ' imported');
  } catch (e) {
    importError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not import the file.';
  } finally {
    importBusy.value = false;
  }
}

async function load(showErrorToast = true): Promise<boolean> {
  try {
    packages.value = await api.packages();
    return true;
  } catch {
    if (showErrorToast) toast.error('We could not load the packages. Please refresh and try again.');
    return false;
  } finally {
    loading.value = false;
  }
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
  formError.value = '';
  saving.value = true;
  const wasEditing = editingCode.value !== null;
  const name = form.packageName || 'Package';
  const payload: PackageInput = { ...form };
  try {
    if (editingCode.value) {
      await api.updatePackage(editingCode.value, payload);
    } else {
      await api.createPackage(payload);
    }
    await load();
    pendingRecovery.value = null;
    historyVersion.value += 1;
    showForm.value = false;
    toast.success(wasEditing ? `${name} updated` : `Package ${name} added`);
  } catch (e) {
    if (!wasEditing && (!(e instanceof ApiError) || e.status >= 500 || e.status === 409)) {
      if (await recoverCreatedPackage(payload)) return;
    }
    if (e instanceof ApiError && e.status === 409) {
      const message = conflictMessage(e, 'package');
      if (editingCode.value && (e.code === 'stale_revision' || e.code === 'write_conflict')) {
        if (await refreshOpenPackageAfterConflict(message)) return;
      }
      await load();
    }
    const message = e instanceof ApiError ? conflictMessage(e, 'package') : 'Could not save the package.';
    formError.value = message;
  } finally {
    saving.value = false;
  }
}

function samePackage(saved: PackageRow, submitted: PackageInput): boolean {
  return saved.id === submitted.id
    && saved.packageName === submitted.packageName
    && saved.destination === submitted.destination
    && (saved.duration ?? '') === (submitted.duration ?? '')
    && saved.basePrice === submitted.basePrice
    && (saved.inclusions ?? '') === (submitted.inclusions ?? '')
    && saved.availableSlots === submitted.availableSlots
    && saved.soldCount === submitted.soldCount
    && saved.reservedCount === submitted.reservedCount
    && saved.status === submitted.status;
}

function applyCreatedPackageRecovery(payload: PackageInput): boolean {
  const saved = packages.value.find((p) => p.id === payload.id);
  if (!saved) return false;

  imageError.value = '';
  if (samePackage(saved, payload)) {
    showForm.value = false;
    toast.success(`Package ${saved.packageName} added`);
    return true;
  }

  loadPackageIntoForm(saved);
  editingCode.value = saved.id;
  const message = 'A package with this code is already saved with different details. Review the saved package before changing it.';
  formError.value = message;
  return true;
}

async function recoverCreatedPackage(payload: PackageInput): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'create', payload };
    formError.value = createRecoveryError;
    return true;
  }
  return applyCreatedPackageRecovery(payload);
}

function applyPackageConflictRefresh(message: string): boolean {
  const saved = editingPackage.value;
  if (!saved) return false;

  loadPackageIntoForm(saved);
  imageError.value = '';
  formError.value = message;
  return true;
}

async function refreshOpenPackageAfterConflict(message: string): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'stale-edit', message };
    formError.value = staleRecoveryError;
    return true;
  }
  return applyPackageConflictRefresh(message);
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
    if (applyCreatedPackageRecovery(pending.payload)) return true;
    formError.value = 'The package was not found after reloading. Review the draft, then save again if it still needs to be created.';
    return true;
  }

  if (applyPackageConflictRefresh(pending.message)) return true;
  formError.value = 'The package was not found after reloading. Refresh the page before making another change.';
  return true;
}

const loadError = ref(false);

/** The first load failed: say so, rather than showing an empty list as if there were no records. */
async function retryLoad() {
  loadError.value = false;
  loading.value = true;
  if (!(await load())) loadError.value = true;
}

onMounted(async () => {
  if (!(await load())) loadError.value = true;
});
</script>

<style scoped>
.package-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.package-thumb,
.package-thumb-space {
  flex: none;
  width: 40px;
  height: 40px;
  border-radius: var(--radius-md);
}

.package-thumb {
  padding: 0;
  overflow: hidden;
  border: 1px solid var(--color-border);
  background: var(--color-surface-sunken);
  cursor: zoom-in;
}

.package-thumb img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.package-names {
  display: grid;
  min-width: 0;
}

.place-name {
  display: block;
  color: var(--color-text-primary);
}

/* How full each package is: sold in navy, reserved in amber, what is left stays open. */
.slots-cell {
  min-width: 200px;
}

.slot-meter {
  display: flex;
  width: 100%;
  max-width: 220px;
  height: 6px;
  overflow: hidden;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
}

.slot-sold {
  background: var(--color-primary);
}

.slot-reserved {
  background: var(--color-warning);
}

.slot-text {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-top: 6px;
  font-size: var(--text-sm);
  font-variant-numeric: lining-nums tabular-nums;
}

.slots-left {
  color: var(--color-text-primary);
  font-weight: 600;
}

.slots-left.tone-warning {
  color: var(--color-warning-ink);
}

.slots-left.tone-danger {
  color: var(--color-danger-ink);
}

.slot-detail {
  color: var(--color-text-muted);
}

.slot-line {
  display: block;
  padding: 4px 0 2px;
}

.slot-line .slot-meter {
  max-width: none;
  height: 4px;
}

.form-history {
  margin-top: 28px;
  padding-top: 20px;
  border-top: 1px solid var(--color-border-subtle);
}

.image-row {
  display: flex;
  align-items: center;
  gap: var(--space-4);
}

.image-preview {
  display: grid;
  place-items: center;
  flex: none;
  width: 132px;
  height: 84px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface-sunken);
  overflow: hidden;
}

.image-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-preview-zoom {
  font: inherit;
  padding: 0;
  cursor: zoom-in;
}

.image-actions {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.image-hint {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.image-error {
  margin: 0;
  color: var(--color-danger-ink);
  font-size: var(--text-sm);
}
</style>
