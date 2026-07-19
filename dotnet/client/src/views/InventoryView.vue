<template>
  <AppShell title="Package Catalog" description="Travel packages and available slots.">
    <template #actions>
      <a class="secondary-button" href="/api/export/packages.csv">Export CSV</a>
      <button class="secondary-button" type="button" @click="openImport">Import from file</button>
      <button class="primary-button" type="button" @click="openForm">Add package</button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="editingCode ? 'Edit package' : 'New package'" @close="showForm = false">
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
                <span class="image-preview">
                  <img v-if="editingImage" :src="editingImage" alt="" />
                  <span v-else class="image-preview-empty">No photo</span>
                </span>

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
                    {{ imageBusy ? 'Uploading…' : editingImage ? 'Replace photo' : 'Upload photo' }}
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

      <div v-if="loading" class="content-panel">
        <div class="panel-header">
          <div class="panel-title-group">
            <h2>Package catalog</h2>
          </div>
          <span class="panel-meta">Loading…</span>
        </div>
        <Skeleton :rows="6" />
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

      <template v-else>
        <section class="stat-band" aria-label="Package catalog totals">
          <div class="stat-cell">
            <span class="stat-label">Total packages</span>
            <strong class="stat-value">{{ packages.length }}</strong>
            <span class="stat-note">In catalog</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Available slots</span>
            <strong class="stat-value">{{ totalAvailable }}</strong>
            <span class="stat-note">Open across packages</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Sold slots</span>
            <strong class="stat-value">{{ totalSold }}</strong>
            <span class="stat-note">Booked to date</span>
          </div>
        </section>

        <div class="catalog-toolbar">
          <div class="catalog-toolbar-heading">
            <span class="catalog-toolbar-title">Every package</span>
            <span class="catalog-toolbar-count">{{ resultLabel }}</span>
          </div>
          <SearchField v-model="query" placeholder="Search packages or destinations" />
        </div>

        <template v-if="filtered.length > 0">
          <div class="package-grid">
            <button
              v-for="p in visiblePackages"
              :key="p.id"
              type="button"
              class="package-card"
              @click="openEdit(p)"
            >
              <span class="package-figure">
                <img v-if="p.imageUrl" :src="p.imageUrl" alt="" loading="lazy" decoding="async" />
                <svg
                  v-else
                  class="package-figure-mark"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.3"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <path d="M3.5 6.5h17v13h-17z" />
                  <path d="M3.5 16 8 11.5l3.5 3 3-2.5 6 5" />
                  <circle cx="9" cy="9.75" r="1.25" />
                </svg>
              </span>

              <span class="package-head">
                <span class="package-title">
                  <span class="package-name">{{ p.packageName }}</span>
                  <span class="package-code">{{ p.id }}</span>
                </span>
                <span class="record-badge" :class="badge(p.status)">{{ p.status }}</span>
              </span>

              <span class="package-meta">
                <span class="package-dest">
                  <svg class="package-pin" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M12 21s7-5.686 7-11a7 7 0 1 0-14 0c0 5.314 7 11 7 11z" />
                    <circle cx="12" cy="10" r="2.5" />
                  </svg>
                  {{ p.destination }}
                </span>
                <span v-if="p.duration" class="package-duration">{{ p.duration }}</span>
              </span>

              <strong class="package-price">{{ peso(p.basePrice) }}</strong>

              <span class="package-slots">
                <span class="slots-meter">
                  <span
                    class="slots-meter-fill"
                    :class="fillModifier(p.status)"
                    :style="{ width: slotsPercent(p) + '%' }"
                  ></span>
                </span>
                <span class="slots-caption">{{ p.availableSlots }} slots left · {{ p.soldCount }} sold</span>
              </span>
            </button>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            noun="packages"
            @more="loadMore"
            @all="showAll"
          />
        </template>

        <EmptyState
          v-else
          title="No matches"
          :message="`No packages match “${query.trim()}”. Try a different name, code, or destination.`"
        >
          <template #action>
            <button class="secondary-button" type="button" @click="query = ''">Clear search</button>
          </template>
        </EmptyState>
      </template>
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
import { api, ApiError, type PackageImportPreview, type PackageRow, type PackageInput } from '../api';
import { count, peso } from '../format';
import MoneyField from '../components/MoneyField.vue';

const toast = useToast();

const packages = ref<PackageRow[]>([]);
const loading = ref(true);
const query = ref('');
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editingCode = ref<string | null>(null);

const form = reactive<PackageInput>(blank());

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

const totalAvailable = computed(() => packages.value.reduce((sum, p) => sum + p.availableSlots, 0));
const totalSold = computed(() => packages.value.reduce((sum, p) => sum + p.soldCount, 0));

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return packages.value;
  return packages.value.filter((p) =>
    p.packageName.toLowerCase().includes(q) ||
    p.id.toLowerCase().includes(q) ||
    (p.destination ?? '').toLowerCase().includes(q)
  );
});

const { visible: visiblePackages, total, shown, loadMore, showAll } = usePaged(filtered);

const resultLabel = computed(() => {
  const count = packages.value.length;
  if (query.value.trim()) return `${filtered.value.length} of ${count} shown`;
  return count === 1 ? '1 package' : `${count} packages`;
});

const fileInput = ref<HTMLInputElement | null>(null);
const imageBusy = ref(false);
const imageError = ref('');

const editingImage = computed(
  () => packages.value.find((p) => p.id === editingCode.value)?.imageUrl ?? null,
);

function replaceRow(updated: PackageRow) {
  const index = packages.value.findIndex((p) => p.id === updated.id);
  if (index >= 0) packages.value[index] = updated;
}

async function pickImage(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  const code = editingCode.value;
  input.value = '';
  if (!file || !code) return;

  imageError.value = '';
  imageBusy.value = true;
  try {
    replaceRow(await api.uploadPackageImage(code, file));
    toast.success('Photo updated.');
  } catch (error) {
    imageError.value =
      error instanceof ApiError
        ? error.fields.image ?? error.message
        : 'Could not upload that photo.';
  } finally {
    imageBusy.value = false;
  }
}

async function dropImage() {
  const code = editingCode.value;
  if (!code) return;

  imageError.value = '';
  imageBusy.value = true;
  try {
    replaceRow(await api.removePackageImage(code));
    toast.success('Photo removed.');
  } catch {
    imageError.value = 'Could not remove that photo.';
  } finally {
    imageBusy.value = false;
  }
}


function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function fillModifier(status: string): string {
  const key = status.toLowerCase();
  if (key === 'critical') return 'is-critical';
  if (key === 'low') return 'is-low';
  return '';
}

function slotsPercent(p: PackageRow): number {
  const total = Math.max(p.availableSlots + p.soldCount + p.reservedCount, 1);
  const ratio = p.availableSlots / total;
  return Math.min(100, Math.max(0, Math.round(ratio * 100)));
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
  };
}

function openForm() {
  Object.assign(form, blank());
  form.id = 'PKG-' + (100 + packages.value.length + 1);
  editingCode.value = null;
  formError.value = '';
  imageError.value = '';
  showForm.value = true;
}

function openEdit(p: PackageRow) {
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
  });
  editingCode.value = p.id;
  formError.value = '';
  imageError.value = '';
  showForm.value = true;
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

async function load() {
  loading.value = true;
  try {
    packages.value = await api.packages();
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  saving.value = true;
  const wasEditing = editingCode.value !== null;
  const name = form.packageName || 'Package';
  try {
    if (editingCode.value) {
      await api.updatePackage(editingCode.value, { ...form });
    } else {
      await api.createPackage({ ...form });
    }
    await load();
    showForm.value = false;
    toast.success(wasEditing ? `${name} updated` : `Package ${name} added`);
  } catch (e) {
    const message = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the package.';
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

.catalog-toolbar {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--space-3) var(--space-4);
}

.catalog-toolbar-heading {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
  min-width: 0;
}

.catalog-toolbar-title {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 18px;
  font-weight: 560;
  letter-spacing: -0.005em;
  color: var(--color-text-primary);
}

.catalog-toolbar-count {
  color: var(--color-text-muted);
  font-size: 12.5px;
  font-variant-numeric: lining-nums tabular-nums;
  white-space: nowrap;
}

.package-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: var(--space-4);
}

.package-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  width: 100%;
  text-align: left;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-4);
  cursor: pointer;
  font: inherit;
  color: inherit;
  transition: transform var(--transition-fast), box-shadow var(--transition-fast), border-color var(--transition-fast);
}

.package-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
  border-color: var(--color-border-strong);
}

.package-card:focus-visible {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.package-figure {
  display: grid;
  place-items: center;
  height: 124px;
  margin: calc(var(--space-4) * -1) calc(var(--space-4) * -1) var(--space-1);
  border-bottom: 1px solid var(--color-ticket-border);
  border-radius: calc(var(--radius-lg) - 1px) calc(var(--radius-lg) - 1px) 0 0;
  background: var(--color-ticket);
  overflow: hidden;
}

.package-figure img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.package-figure-mark {
  width: 30px;
  height: 30px;
  color: #C4AC7A;
}

.package-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.package-title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.package-name {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 1.1rem;
  font-weight: 560;
  letter-spacing: -0.005em;
  line-height: 1.25;
  color: var(--color-text-primary);
}

.package-code {
  color: var(--color-text-muted);
  font-size: 12px;
  letter-spacing: 0.02em;
  font-variant-numeric: lining-nums tabular-nums;
}

.package-head .record-badge {
  flex: none;
}

.package-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 6px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.package-dest {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
}

.package-pin {
  width: 14px;
  height: 14px;
  flex: none;
  color: var(--color-text-muted);
}

.package-duration::before {
  content: '·';
  margin: 0 6px 0 0;
  color: var(--color-text-faint);
}

.package-price {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 1.25rem;
  font-weight: 560;
  letter-spacing: -0.01em;
  line-height: 1.2;
  color: var(--color-text-primary);
  font-variant-numeric: lining-nums tabular-nums;
}

.package-slots {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin-top: auto;
}

.slots-meter {
  display: block;
  width: 100%;
  height: 6px;
  background: var(--color-surface-sunken);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.slots-meter-fill {
  display: block;
  height: 100%;
  min-width: 2px;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
}

.slots-meter-fill.is-low {
  background: var(--color-warning);
}

.slots-meter-fill.is-critical {
  background: var(--color-danger);
}

.slots-caption {
  color: var(--color-text-muted);
  font-size: 12px;
  font-variant-numeric: lining-nums tabular-nums;
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
  border: 1px solid var(--color-ticket-border);
  border-radius: var(--radius-md);
  background: var(--color-ticket);
  overflow: hidden;
}

.image-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-preview-empty {
  color: var(--color-text-muted);
  font-size: 12px;
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
  font-size: 12.5px;
}

.image-error {
  margin: 0;
  color: var(--color-danger-ink);
  font-size: 12.5px;
}

@media (max-width: 640px) {
  .catalog-toolbar {
    align-items: stretch;
  }

  .catalog-toolbar :deep(.search-field) {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .package-card {
    transition: none;
  }

  .package-card:hover {
    transform: none;
  }
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
