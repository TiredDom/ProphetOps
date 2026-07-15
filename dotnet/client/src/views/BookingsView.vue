<template>
  <AppShell title="Bookings" description="Booking and transaction records.">
    <template #actions>
      <button class="primary-button" type="button" @click="openForm">Add booking</button>
    </template>

    <section class="dss-page">
      <section class="stat-band" aria-label="Booking totals">
        <div class="stat-cell">
          <span class="stat-label">Records</span>
          <strong class="stat-value">{{ bookings.length }}</strong>
          <span class="stat-note">Saved bookings</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Passengers</span>
          <strong class="stat-value">{{ totalPassengers }}</strong>
          <span class="stat-note">Across all records</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Booked revenue</span>
          <strong class="stat-value">{{ peso(totalRevenue) }}</strong>
          <span class="stat-note">Gross, all records</span>
        </div>
      </section>

      <div v-if="showForm" class="booking-form-panel">
        <h2>New booking</h2>
        <p v-if="formError" class="booking-form-error" role="alert">{{ formError }}</p>
        <div class="form-grid">
          <label class="account-field">
            <span>Booking date</span>
            <input v-model="form.ds" type="date" />
          </label>
          <label class="account-field">
            <span>Client / agency partner</span>
            <input v-model.trim="form.client" maxlength="120" />
          </label>
          <label class="account-field">
            <span>Package</span>
            <select v-model="form.packageId" @change="applyPackage">
              <option v-for="p in packages" :key="p.id" :value="p.id">{{ p.packageName }}</option>
            </select>
          </label>
          <label class="account-field">
            <span>Passengers</span>
            <input v-model.number="form.y" type="number" min="1" @input="applyPackage" />
          </label>
          <label class="account-field">
            <span>Gross revenue</span>
            <input v-model.number="form.grossRevenue" type="number" min="0" />
          </label>
          <label class="account-field">
            <span>Payment status</span>
            <select v-model="form.paymentStatus">
              <option>Paid</option>
              <option>Partially Paid</option>
              <option>Pending</option>
            </select>
          </label>
          <label class="account-field">
            <span>Booking status</span>
            <select v-model="form.bookingStatus">
              <option>Confirmed</option>
              <option>Reserved</option>
              <option>Pending</option>
            </select>
          </label>
        </div>
        <div class="booking-form-actions">
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : 'Save booking' }}
          </button>
        </div>
      </div>

      <div class="dss-table-frame">
        <table class="dss-table">
          <thead>
            <tr>
              <th>Booking ID</th>
              <th>Client / Partner</th>
              <th>Package</th>
              <th class="num">Passengers</th>
              <th class="num">Gross revenue</th>
              <th>Payment</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="b in bookings" :key="b.id">
              <td><strong>{{ b.id }}</strong></td>
              <td>{{ b.client }}</td>
              <td><strong>{{ b.package }}</strong><span class="row-subtext">{{ b.destination }}</span></td>
              <td class="num">{{ b.y }}</td>
              <td class="num"><strong>{{ peso(b.grossRevenue) }}</strong></td>
              <td><span class="record-badge" :class="badge(b.paymentStatus)">{{ b.paymentStatus }}</span></td>
              <td><span class="record-badge" :class="badge(b.bookingStatus)">{{ b.bookingStatus }}</span></td>
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
import { api, ApiError, type Booking, type BookingInput, type PackageOption } from '../api';

const bookings = ref<Booking[]>([]);
const packages = ref<PackageOption[]>([]);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');

const form = reactive<BookingInput>(blank());

const totalPassengers = computed(() => bookings.value.reduce((sum, b) => sum + b.y, 0));
const totalRevenue = computed(() => bookings.value.reduce((sum, b) => sum + b.grossRevenue, 0));

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function blank(): BookingInput {
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
  };
}

function applyPackage() {
  const preset = packages.value.find((p) => p.id === form.packageId);
  if (!preset) return;
  form.package = preset.packageName;
  form.destination = preset.destination;
  form.grossRevenue = Number(form.y || 1) * preset.basePrice;
}

function openForm() {
  Object.assign(form, blank());
  form.id = 'BKG-' + (2400 + bookings.value.length + 1);
  form.packageId = packages.value[0]?.id ?? null;
  applyPackage();
  formError.value = '';
  showForm.value = true;
}

async function load() {
  const payload = await api.bookings();
  bookings.value = payload.bookings;
  packages.value = payload.packages;
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  saving.value = true;
  try {
    await api.createBooking({ ...form });
    await load();
    showForm.value = false;
  } catch (e) {
    formError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the booking.';
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<style scoped>
.booking-form-panel {
  margin: 1.5rem 0;
  padding: 1.5rem 1.75rem;
  background: var(--color-surface, #FFFFFF);
  border: 1px solid rgba(21, 34, 27, 0.14);
  border-radius: 10px;
}
.booking-form-panel h2 {
  margin: 0 0 1rem;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 1.3rem;
}
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}
.booking-form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 1.25rem;
}
.booking-form-error {
  margin: 0 0 1rem;
  color: #B42318;
}
</style>
