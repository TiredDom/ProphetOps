<template>
  <AppShell title="Dashboard" description="Business overview and revenue outlook.">
    <section class="dss-page">
      <!-- Loading Skeleton State -->
      <div v-if="loading" class="dash-loading-grid" aria-busy="true" aria-label="Loading dashboard data">
        <section class="overview-skeleton-card">
          <div class="skeleton-line sm w-25"></div>
          <div class="skeleton-box chart-skeleton-lg"></div>
        </section>

        <section class="stat-band">
          <div v-for="n in 4" :key="n" class="stat-cell">
            <div class="skeleton-line sm w-40"></div>
            <div class="skeleton-line xl w-70"></div>
            <div class="skeleton-line sm w-90"></div>
          </div>
        </section>

        <section class="content-panel">
          <div class="skeleton-line md w-35"></div>
          <div class="skeleton-line sm w-90" style="margin-top: 14px"></div>
          <div class="skeleton-line sm w-80" style="margin-top: 8px"></div>
        </section>
      </div>

      <!-- Error State -->
      <section v-else-if="error" class="dash-error-panel" role="alert">
        <div class="dash-error-content">
          <h3 class="dash-error-title">Could not load dashboard data</h3>
          <p class="dash-error-message">{{ error }}</p>
        </div>
        <button type="button" class="btn btn-secondary btn-sm" @click="loadData">
          Retry
        </button>
      </section>

      <!-- Loaded Dashboard Content -->
      <template v-else-if="data">
        <!-- 1. MAIN Full-Width Highlight: Approved Forecast Overview -->
        <div class="dash-overview-highlight">
          <!-- Active Forecast Overview (Graph + Attached Recommendations) -->
          <ForecastOverviewCard
            v-if="forecastData && forecastData.ok"
            :data="forecastData"
            :show-recommendations="true"
            :show-forecast-link="true"
            title="Revenue outlook"
          />

          <!-- Insufficient History / Forecast Unavailable Local State -->
          <section
            v-else-if="forecastData && !forecastData.ok"
            class="content-panel revenue-panel revenue-unavailable-container"
            aria-label="Revenue outlook status"
          >
            <div class="revenue-unavailable-panel">
              <h3 class="revenue-unavailable-title">Forecast unavailable</h3>
              <p class="revenue-unavailable-desc">
                Live Holt-Winters forecasting requires 24 recorded booking months to calculate seasonal patterns and trend. Currently {{ recordedMonths }} of 24 required months are on record.
              </p>
              <p class="revenue-unavailable-note">
                Forecasts recorded booking revenue in ₱, not passenger volume or travel demand.
              </p>
              <RouterLink to="/forecast" class="compact-action-link">
                View forecast requirements <span aria-hidden="true">→</span>
              </RouterLink>
            </div>
          </section>

          <!-- Forecast Load Error Local State -->
          <section
            v-else-if="forecastError"
            class="content-panel revenue-panel revenue-unavailable-container"
            aria-label="Revenue outlook error"
          >
            <div class="revenue-unavailable-panel">
              <h3 class="revenue-unavailable-title">Forecast unavailable</h3>
              <p class="revenue-unavailable-desc">{{ forecastError }}</p>
              <RouterLink to="/forecast" class="compact-action-link">
                View forecast requirements <span aria-hidden="true">→</span>
              </RouterLink>
            </div>
          </section>
        </div>

        <!-- 2. Compact Business Totals Snapshot -->
        <section class="stat-band dashboard-totals" aria-label="Lifetime business totals, excluding voided records">
          <div class="stat-cell">
            <span class="stat-label">Gross Revenue</span>
            <strong class="stat-value">{{ peso(data.revenue) }}</strong>
            <span class="stat-note">Lifetime gross, excluding voided bookings</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Recorded Costs</span>
            <strong class="stat-value">{{ peso(data.costs) }}</strong>
            <span class="stat-note">Lifetime expenses, excluding voided entries</span>
          </div>
          <div class="stat-cell">
            <span class="stat-label">Estimated Profit</span>
            <strong class="stat-value" :class="{ 'val-negative': data.estimatedProfit < 0 }">
              {{ peso(data.estimatedProfit) }}
            </strong>
            <span class="stat-note">Gross revenue minus recorded expenses</span>
          </div>
          <div class="stat-cell" :class="{ warn: pendingCount > 0 }">
            <span class="stat-label">Unpaid Booking Value</span>
            <strong class="stat-value">{{ peso(data.pendingPayments.amount) }}</strong>
            <span class="stat-note">
              {{ pendingCount }} unpaid {{ pendingCount === 1 ? 'booking' : 'bookings' }}
            </span>
          </div>
        </section>

        <!-- 3. Operational: Needs Attention Items -->
        <section class="content-panel attention-panel" aria-label="Needs attention">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Needs attention</h2>
              <span class="panel-meta">Immediate action items</span>
            </div>
          </div>

          <div class="attention-rows-list">
            <!-- Item 1: Low available slots -->
            <div class="attention-row">
              <div class="attention-row-header">
                <span class="attention-row-title">Low available slots</span>
                <span v-if="lowCount > 0" class="attention-count-badge warn">{{ lowCount }}</span>
                <span v-else class="attention-count-badge ok">0</span>
              </div>
              <div v-if="data.lowStockPackages.length" class="attention-item-content">
                <p class="attention-row-desc">
                  {{ lowCount === 1 ? '1 package is' : `${lowCount} packages are` }} running low on available slots:
                </p>
                <ul class="attention-sublist">
                  <li v-for="p in data.lowStockPackages" :key="p.code">
                    <span class="package-name">{{ p.packageName }}</span>
                    <span class="package-slots">({{ p.availableSlots }} left)</span>
                  </li>
                </ul>
                <RouterLink to="/inventory" class="compact-action-link">
                  Review package capacity <span aria-hidden="true">→</span>
                </RouterLink>
              </div>
              <div v-else class="attention-clean-state">
                <p class="clean-text">All package slot allocations look healthy.</p>
              </div>
            </div>

            <!-- Item 2: Unpaid bookings -->
            <div class="attention-row">
              <div class="attention-row-header">
                <span class="attention-row-title">Unpaid bookings</span>
                <span v-if="pendingCount > 0" class="attention-count-badge warn">{{ pendingCount }}</span>
                <span v-else class="attention-count-badge ok">0</span>
              </div>
              <div v-if="pendingCount > 0" class="attention-item-content">
                <p class="attention-row-desc">
                  <strong>{{ peso(data.pendingPayments.amount) }}</strong> outstanding across {{ pendingCount }} {{ pendingCount === 1 ? 'booking' : 'bookings' }}.
                </p>
                <RouterLink to="/bookings" class="compact-action-link">
                  Follow up on payments <span aria-hidden="true">→</span>
                </RouterLink>
              </div>
              <div v-else class="attention-clean-state">
                <p class="clean-text">All booking payments are settled and up to date.</p>
              </div>
            </div>
          </div>
        </section>

        <!-- 4. Operational: Recent Bookings Table -->
        <section class="content-panel bookings-panel" aria-label="Recent bookings">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Recent bookings</h2>
              <span class="panel-meta">Latest 5 recorded bookings</span>
            </div>
            <RouterLink class="table-link" to="/bookings">
              View all bookings <span aria-hidden="true">→</span>
            </RouterLink>
          </div>

          <div v-if="data.recentBookings.length" class="table-scroll">
            <table class="dss-table">
              <thead>
                <tr>
                  <th scope="col">Client / Partner</th>
                  <th scope="col">Package</th>
                  <th scope="col" class="num">Revenue</th>
                  <th scope="col">Payment</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="b in data.recentBookings" :key="b.code">
                  <td>
                    <strong>{{ b.client }}</strong>
                    <span class="row-subtext">{{ b.ds }}</span>
                  </td>
                  <td>
                    <strong>{{ b.package }}</strong>
                    <span class="row-subtext">{{ b.destination }}</span>
                  </td>
                  <td class="num">
                    <strong>{{ peso(b.grossRevenue) }}</strong>
                  </td>
                  <td>
                    <span class="record-badge" :class="badge(b.paymentStatus)">
                      {{ b.paymentStatus }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div v-else class="empty-state">
            <h4>No bookings yet</h4>
            <p>New bookings will appear here as staff record them.</p>
          </div>
        </section>

        <!-- Footer timestamp -->
        <p class="dash-foot">Updated {{ data.lastUpdated }}</p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import ForecastOverviewCard from '../components/ForecastOverviewCard.vue';
import { api, type DashboardData, type ForecastData } from '../api';
import { peso } from '../format';

const data = ref<DashboardData | null>(null);
const forecastData = ref<ForecastData | null>(null);
const loading = ref(true);
const error = ref('');
const forecastError = ref('');

const lowCount = computed(() => data.value?.lowStockPackages.length ?? 0);
const pendingCount = computed(() => data.value?.pendingPayments.count ?? 0);

const recordedMonths = computed(() => {
  return forecastData.value?.dataSource.recordedMonths ?? data.value?.forecast.dataSource.recordedMonths ?? 0;
});

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

async function loadData() {
  loading.value = true;
  error.value = '';
  forecastError.value = '';
  try {
    const [dashRes, forecastRes] = await Promise.allSettled([
      api.dashboard(),
      api.forecast(),
    ]);

    if (dashRes.status === 'fulfilled') {
      data.value = dashRes.value;
    } else {
      throw new Error(dashRes.reason?.message || 'Could not load dashboard data.');
    }

    if (forecastRes.status === 'fulfilled') {
      forecastData.value = forecastRes.value;
    } else {
      forecastError.value = forecastRes.reason?.message || 'Could not load forecast data.';
    }
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Could not load dashboard data.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  loadData();
});
</script>

<style scoped>
/* ============================================================
   Dashboard Layout & Overview Highlight
   ============================================================ */
.dash-overview-highlight {
  margin-bottom: var(--space-4);
}

.revenue-unavailable-container {
  margin-bottom: var(--space-4);
}

.revenue-unavailable-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 6px 0 2px;
}

.revenue-unavailable-title {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.revenue-unavailable-desc {
  margin: 0;
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--color-text-secondary);
}

.revenue-unavailable-note {
  margin: 0;
  font-size: 12.5px;
  color: var(--color-text-muted);
}

.compact-action-link {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--color-primary);
  font-size: 13.5px;
  font-weight: 600;
  text-decoration: none;
  border-radius: var(--radius-sm);
  padding: 3px 0;
}

.compact-action-link:hover {
  color: var(--color-primary-hover);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.compact-action-link:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: 2px;
}

/* ============================================================
   Needs Attention — Flat Rows
   ============================================================ */
.attention-panel {
  margin-bottom: var(--space-4);
}

.attention-rows-list {
  display: flex;
  flex-direction: column;
}

.attention-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 0;
  border-bottom: 1px solid var(--color-border-subtle);
}

.attention-row:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.attention-row:first-child {
  padding-top: 0;
}

.attention-row-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.attention-row-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.attention-count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 20px;
  padding: 0 6px;
  border-radius: var(--radius-pill);
  font-size: 11.5px;
  font-weight: 700;
  font-variant-numeric: lining-nums tabular-nums;
}

.attention-count-badge.warn {
  background: var(--color-accent-soft);
  color: var(--color-accent-ink);
}

.attention-count-badge.ok {
  background: var(--tone-neutral-surface);
  color: var(--color-text-muted);
}

.attention-item-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.attention-row-desc {
  margin: 0;
  font-size: 13.5px;
  color: var(--color-text-secondary);
  line-height: 1.45;
}

.attention-sublist {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.attention-sublist li {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 13px;
}

.package-name {
  font-weight: 500;
  color: var(--color-text-primary);
}

.package-slots {
  color: var(--color-accent-ink);
  font-weight: 600;
}

.attention-clean-state {
  padding: 2px 0;
}

.clean-text {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-muted);
}

/* ============================================================
   Recent Bookings Panel
   ============================================================ */
.bookings-panel {
  margin-bottom: var(--space-4);
}

/* ============================================================
   Loading & Skeletons
   ============================================================ */
.dash-loading-grid {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.overview-skeleton-card {
  padding: 20px 24px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.chart-skeleton-lg {
  height: 240px;
}

/* ============================================================
   Error State
   ============================================================ */
.dash-error-panel {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: 16px 20px;
  background: var(--color-danger-subtle);
  border: 1px solid var(--color-danger-border);
  border-radius: var(--radius-md);
  margin-bottom: var(--space-5);
}

.dash-error-title {
  margin: 0 0 4px;
  font-size: 14.5px;
  font-weight: 600;
  color: var(--color-danger);
}

.dash-error-message {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.dash-foot {
  margin-top: var(--space-4);
  font-size: 12px;
  color: var(--color-text-muted);
  text-align: right;
}
</style>
