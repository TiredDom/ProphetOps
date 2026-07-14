<template>
  <main class="dash">
    <header class="dash-top">
      <div>
        <p class="dash-brand">ProphetOps</p>
        <h1>Dashboard</h1>
      </div>
      <div class="dash-user">
        <span>{{ state.user?.name }} · {{ state.user?.role }}</span>
        <button class="dash-signout" type="button" @click="signOut">Sign out</button>
      </div>
    </header>

    <p v-if="loading" class="dash-note">Loading live data…</p>
    <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

    <template v-else-if="data">
      <section class="dash-grid">
        <article class="stat"><span>Revenue</span><strong>{{ peso(data.revenue) }}</strong></article>
        <article class="stat"><span>Costs</span><strong>{{ peso(data.costs) }}</strong></article>
        <article class="stat"><span>Estimated profit</span><strong>{{ peso(data.estimatedProfit) }}</strong></article>
        <article class="stat"><span>Bookings</span><strong>{{ data.bookings }}</strong></article>
        <article class="stat"><span>Packages</span><strong>{{ data.packages }}</strong></article>
        <article class="stat"><span>Expenses</span><strong>{{ data.expenses }}</strong></article>
      </section>

      <section class="forecast">
        <p class="forecast-label">Demand forecast · {{ data.forecast.method }}</p>
        <p class="forecast-headline">{{ data.forecast.accuracy }}% in-sample fit</p>
        <p class="forecast-detail">
          MAPE {{ data.forecast.mape }}% · next month ≈ {{ peso(data.forecast.nextValue) }} ·
          {{ data.forecast.horizon }}-month horizon
        </p>
      </section>

      <p class="dash-foot">Updated {{ data.lastUpdated }} · served by the .NET API</p>
    </template>
  </main>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, type DashboardData } from '../api';
import { useAuth } from '../stores/auth';

const router = useRouter();
const { state, logout } = useAuth();

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

async function signOut() {
  await logout();
  await router.replace('/login');
}
</script>

<style scoped>
.dash {
  min-height: 100vh;
  padding: 2.5rem clamp(1.25rem, 5vw, 4.5rem);
  background: var(--color-bg, #F5F4EF);
  color: #15221B;
  font-family: 'Inter Variable', system-ui, sans-serif;
}
.dash-top {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
  border-bottom: 1px solid rgba(21, 34, 27, 0.12);
  padding-bottom: 1.25rem;
  margin-bottom: 2rem;
}
.dash-brand {
  margin: 0;
  font-size: 0.72rem;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: rgba(21, 34, 27, 0.6);
}
.dash-top h1 {
  margin: 0.15rem 0 0;
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 2rem;
  font-weight: 600;
}
.dash-user {
  display: flex;
  align-items: center;
  gap: 1rem;
  font-size: 0.9rem;
  color: rgba(21, 34, 27, 0.75);
}
.dash-signout {
  border: 1px solid rgba(21, 34, 27, 0.2);
  background: #FFFFFF;
  color: #15221B;
  border-radius: 6px;
  padding: 0.45rem 0.9rem;
  font: inherit;
  cursor: pointer;
}
.dash-signout:hover { background: #15221B; color: #FFFFFF; }
.dash-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1px;
  background: rgba(21, 34, 27, 0.12);
  border: 1px solid rgba(21, 34, 27, 0.12);
  border-radius: 10px;
  overflow: hidden;
}
.stat {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1.4rem 1.5rem;
  background: var(--color-surface, #FFFFFF);
}
.stat span {
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(21, 34, 27, 0.55);
}
.stat strong {
  font-family: 'Fraunces Variable', Georgia, serif;
  font-size: 1.7rem;
  font-weight: 600;
}
.forecast {
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
.forecast-detail { margin: 0; color: rgba(233, 237, 233, 0.8); font-size: 0.95rem; }
.dash-note { margin-top: 2rem; color: rgba(21, 34, 27, 0.7); }
.dash-error { color: #B42318; }
.dash-foot { margin-top: 1.5rem; font-size: 0.8rem; color: rgba(21, 34, 27, 0.5); }
</style>
