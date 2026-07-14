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

      <div v-if="showForm" class="package-form-panel">
        <h2>New package</h2>
        <p v-if="formError" class="package-form-error" role="alert">{{ formError }}</p>
        <div class="form-grid">
          <label class="account-field">
            <span>Package code</span>
            <input v-model.trim="form.id" maxlength="40" />
          </label>
          <label class="account-field">
            <span>Package name</span>
            <input v-model.trim="form.packageName" maxlength="120" />
          </label>
          <label class="account-field">
            <span>Destination</span>
            <input v-model.trim="form.destination" maxlength="120" />
          </label>
          <label class="account-field">
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
          <label class="account-field">
            <span>Status</span>
            <select v-model="form.status">
              <option>Normal</option>
              <option>Low</option>
              <option>Critical</option>
            </select>
          </label>
          <label class="account-field">
            <span>Inclusions</span>
            <input v-model.trim="form.inclusions" maxlength="420" />
          </label>
        </div>
        <div class="package-form-actions">
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : 'Save package' }}
          </button>
        </div>
      </div>

      <div class="dss-table-frame">
        <table class="dss-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Package</th>
              <th>Destination</th>
              <th class="num">Base price</th>
              <th class="num">Slots</th>
              <th class="num">Sold</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in packages" :key="p.id">
              <td><strong>{{ p.id }}</strong></td>
              <td><strong>{{ p.packageName }}</strong><span class="row-subtext">{{ p.duration }}</span></td>
              <td>{{ p.destination }}</td>
              <td class="num"><strong>{{ peso(p.basePrice) }}</strong></td>
              <td class="num">{{ p.availableSlots }}</td>
              <td class="num">{{ p.soldCount }}</td>
              <td><span class="record-badge" :class="badge(p.status)">{{ p.status }}</span></td>
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
import { api, ApiError, type PackageRow, type PackageInput } from '../api';

const packages = ref<PackageRow[]>([]);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');

const form = reactive<PackageInput>(blank());

const totalAvailable = computed(() => packages.value.reduce((sum, p) => sum + p.availableSlots, 0));
const totalSold = computed(() => packages.value.reduce((sum, p) => sum + p.soldCount, 0));

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
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
    await api.createPackage({ ...form });
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
.package-form-panel {
  margin: 1.5rem 0;
  padding: 1.5rem 1.75rem;
  background: var(--color-surface, #FFFFFF);
  border: 1px solid rgba(21, 34, 27, 0.14);
  border-radius: 10px;
}
.package-form-panel h2 {
  margin: 0 0 1rem;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 1.3rem;
}
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}
.package-form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 1.25rem;
}
.package-form-error {
  margin: 0 0 1rem;
  color: #B42318;
}
</style>
