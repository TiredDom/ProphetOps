<template>
  <AppShell title="Reports" description="Consolidated business summary.">
    <section class="dss-page">
      <p v-if="loading" class="report-note">Loading live data…</p>
      <p v-else-if="error" class="report-note report-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="stat-band" aria-label="Business summary">
          <div class="stat-cell">
            <span class="stat-label">Revenue</span>
            <strong class="stat-value">{{ peso(data.revenue) }}</strong>
            <span class="stat-note">Gross, all bookings</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Costs</span>
            <strong class="stat-value">{{ peso(data.costs) }}</strong>
            <span class="stat-note">Recorded expenses</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Profit</span>
            <strong class="stat-value">{{ peso(data.profit) }}</strong>
            <span class="stat-note">Revenue minus costs</span>
          </div>
        </section>

        <section class="stat-band" aria-label="Record counts">
          <div class="stat-cell">
            <span class="stat-label">Bookings</span>
            <strong class="stat-value">{{ data.counts.bookings }}</strong>
            <span class="stat-note">Saved records</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Packages</span>
            <strong class="stat-value">{{ data.counts.packages }}</strong>
            <span class="stat-note">In the catalog</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Expenses</span>
            <strong class="stat-value">{{ data.counts.expenses }}</strong>
            <span class="stat-note">Cost entries</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Users</span>
            <strong class="stat-value">{{ data.counts.users }}</strong>
            <span class="stat-note">Team accounts</span>
          </div>
        </section>

        <div class="dss-table-frame">
          <table class="dss-table">
            <thead>
              <tr>
                <th>Booking status</th>
                <th class="num">Bookings</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in data.bookingsByStatus" :key="row.label">
                <td><span class="record-badge" :class="badge(row.label)">{{ row.label }}</span></td>
                <td class="num"><strong>{{ row.value }}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="dss-table-frame">
          <table class="dss-table">
            <thead>
              <tr>
                <th>Expense category</th>
                <th class="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in data.expensesByCategory" :key="row.label">
                <td><strong>{{ row.label }}</strong></td>
                <td class="num"><strong>{{ peso(row.value) }}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="dss-table-frame">
          <table class="dss-table">
            <thead>
              <tr>
                <th>Package</th>
                <th class="num">Revenue</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in data.revenueByPackage" :key="row.label">
                <td><strong>{{ row.label }}</strong></td>
                <td class="num"><strong>{{ peso(row.value) }}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <p class="report-foot">Generated {{ data.generatedAt }}</p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type ReportsData } from '../api';

const data = ref<ReportsData | null>(null);
const loading = ref(true);
const error = ref('');

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

onMounted(async () => {
  try {
    data.value = await api.reports();
  } catch {
    error.value = 'Could not load the reports.';
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.report-note {
  margin: 1.5rem 0;
  color: rgba(21, 34, 27, 0.7);
}
.report-error {
  color: #B42318;
}
.report-foot {
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: rgba(21, 34, 27, 0.5);
}
</style>
