<template>
  <AppShell title="Package Catalog" description="Travel packages and available slots.">
    <template #actions>
      <button class="primary-button" type="button" @click="openForm">Add package</button>
    </template>

    <section class="dss-page">
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
            <input v-model.number="form.basePrice" type="number" min="0" />
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
        </div>
        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : editingCode ? 'Save changes' : 'Save package' }}
          </button>
        </template>
      </Drawer>

      <div v-if="packages.length === 0" class="empty-state">
        <h4>No packages yet</h4>
        <p>Add your first travel package to start tracking available slots and sales.</p>
        <button class="empty-state-action" type="button" @click="openForm">Add package</button>
      </div>

      <div v-else class="package-grid">
        <button
          v-for="p in packages"
          :key="p.id"
          type="button"
          class="package-card"
          @click="openEdit(p)"
        >
          <div class="package-head">
            <span class="package-title">
              <span class="package-name">{{ p.packageName }}</span>
              <span class="package-code">{{ p.id }}</span>
            </span>
            <span class="record-badge" :class="badge(p.status)">{{ p.status }}</span>
          </div>

          <div class="package-meta">
            <span class="package-dest">
              <svg class="package-pin" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M12 21s7-5.686 7-11a7 7 0 1 0-14 0c0 5.314 7 11 7 11z" />
                <circle cx="12" cy="10" r="2.5" />
              </svg>
              {{ p.destination }}
            </span>
            <span v-if="p.duration" class="package-duration">{{ p.duration }}</span>
          </div>

          <strong class="package-price">{{ peso(p.basePrice) }}</strong>

          <div class="package-slots">
            <span class="slots-meter">
              <span
                class="slots-meter-fill"
                :class="fillModifier(p.status)"
                :style="{ width: slotsPercent(p) + '%' }"
              ></span>
            </span>
            <span class="slots-caption">{{ p.availableSlots }} slots left · {{ p.soldCount }} sold</span>
          </div>
        </button>
      </div>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import { api, ApiError, type PackageRow, type PackageInput } from '../api';

const packages = ref<PackageRow[]>([]);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editingCode = ref<string | null>(null);

const form = reactive<PackageInput>(blank());

const totalAvailable = computed(() => packages.value.reduce((sum, p) => sum + p.availableSlots, 0));
const totalSold = computed(() => packages.value.reduce((sum, p) => sum + p.soldCount, 0));

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
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
  showForm.value = true;
}

async function load() {
  packages.value = await api.packages();
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  saving.value = true;
  try {
    if (editingCode.value) {
      await api.updatePackage(editingCode.value, { ...form });
    } else {
      await api.createPackage({ ...form });
    }
    await load();
    showForm.value = false;
  } catch (e) {
    formError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the package.';
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

.package-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: var(--space-4);
}

.package-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  width: 100%;
  text-align: left;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
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

@media (prefers-reduced-motion: reduce) {
  .package-card {
    transition: none;
  }

  .package-card:hover {
    transform: none;
  }
}
</style>
