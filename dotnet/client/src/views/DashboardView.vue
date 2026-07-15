<template>
  <AppShell title="Dashboard" description="Business overview and demand outlook.">
    <section class="dss-page">
      <p v-if="loading" class="dash-note">Loading live data…</p>
      <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section class="stat-band" aria-label="Business totals">
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
            <span class="stat-label">Estimated profit</span>
            <strong class="stat-value">{{ peso(data.estimatedProfit) }}</strong>
            <span class="stat-note">Revenue minus costs</span>
          </div>
        </section>

        <section class="stat-band" aria-label="Record counts">
          <div class="stat-cell">
            <span class="stat-label">Bookings</span>
            <strong class="stat-value">{{ data.bookings }}</strong>
            <span class="stat-note">Saved records</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Packages</span>
            <strong class="stat-value">{{ data.packages }}</strong>
            <span class="stat-note">Catalog presets</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Expenses</span>
            <strong class="stat-value">{{ data.expenses }}</strong>
            <span class="stat-note">Cost entries</span>
          </div>
        </section>

        <section class="forecast-panel">
          <p class="forecast-label">Demand forecast · {{ data.forecast.method }}</p>
          <p class="forecast-headline">{{ data.forecast.accuracy }}% in-sample fit</p>
          <p class="forecast-detail">
            MAPE {{ data.forecast.mape }}% · next month ≈ {{ peso(data.forecast.nextValue) }} ·
            {{ data.forecast.horizon }}-month horizon
          </p>
        </section>

        <p class="dash-foot">Updated {{ data.lastUpdated }} · served by the .NET API</p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type DashboardData } from '../api';

const data = ref<DashboardData | null>(null);
const loading = ref(true);
const error = ref('');

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

onMounted(async () => {
  try {
    data.value = await api.dashboard();
  } catch {
    error.value = 'Could not load the dashboard.';
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.forecast-panel {
  margin-top: 1.5rem;
  padding: 1.75rem 2rem;
  background: #15221B;
  color: #E9EDE9;
  border-radius: 10px;
}
.forecast-label {
  margin: 0;
  font-size: 0.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgba(233, 237, 233, 0.6);
}
.forecast-headline {
  margin: 0.35rem 0 0.25rem;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 2.1rem;
  font-weight: 600;
}
.forecast-detail {
  margin: 0;
  color: rgba(233, 237, 233, 0.8);
  font-size: 0.95rem;
}
.dash-note {
  margin: 1.5rem 0;
  color: rgba(21, 34, 27, 0.7);
}
.dash-error {
  color: #B42318;
}
.dash-foot {
  margin-top: 1.5rem;
  font-size: 0.8rem;
  color: rgba(21, 34, 27, 0.5);
}
</style>
