<template>
  <AppShell title="Dashboard" description="Business overview and demand outlook.">
    <section class="dss-page">
      <p v-if="loading" class="dash-note">Loading live data…</p>
      <p v-else-if="error" class="dash-note dash-error" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <section
          class="review-callout"
          :class="needsAttention ? 'review-callout-warning' : 'review-callout-calm'"
          aria-label="What to review first"
        >
          <div class="review-callout-body">
            <p class="review-callout-eyebrow">{{ needsAttention ? 'Review first' : 'All clear' }}</p>
            <p class="review-callout-line">{{ reviewSentence }}</p>
          </div>
          <div v-if="needsAttention" class="review-callout-actions">
            <RouterLink v-if="lowCount > 0" class="table-link" to="/inventory">Open catalog</RouterLink>
            <RouterLink v-if="pendingCount > 0" class="table-link" to="/bookings">Open bookings</RouterLink>
          </div>
        </section>

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
          <div class="stat-cell" :class="{ warn: pendingCount > 0 }">
            <span class="stat-label">Awaiting payment</span>
            <strong class="stat-value">{{ peso(data.pendingPayments.amount) }}</strong>
            <span class="stat-note">{{ pendingCount }} unpaid {{ pendingCount === 1 ? 'booking' : 'bookings' }}</span>
          </div>
        </section>

        <div class="dss-grid two-column">
          <section class="content-panel">
            <div class="panel-header">
              <div class="panel-title-group">
                <h2>Recent bookings</h2>
                <span class="panel-meta">Latest 5 records</span>
              </div>
              <RouterLink class="table-link" to="/bookings">View all</RouterLink>
            </div>

            <div v-if="data.recentBookings.length" class="table-scroll">
              <table class="dss-table">
                <thead>
                  <tr>
                    <th>Client / Partner</th>
                    <th>Package</th>
                    <th class="num">Revenue</th>
                    <th>Payment</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="b in data.recentBookings" :key="b.code">
                    <td><strong>{{ b.client }}</strong><span class="row-subtext">{{ b.ds }}</span></td>
                    <td><strong>{{ b.package }}</strong><span class="row-subtext">{{ b.destination }}</span></td>
                    <td class="num"><strong>{{ peso(b.grossRevenue) }}</strong></td>
                    <td><span class="record-badge" :class="badge(b.paymentStatus)">{{ b.paymentStatus }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="empty-state">
              <h4>No bookings yet</h4>
              <p>New bookings will show up here as staff record them.</p>
            </div>
          </section>

          <section class="content-panel">
            <div class="panel-header">
              <div class="panel-title-group">
                <h2>Needs attention</h2>
              </div>
            </div>

            <span class="section-label attention-sublabel">Running low on slots</span>
            <div v-if="data.lowStockPackages.length" class="dashboard-review-list">
              <div v-for="p in data.lowStockPackages" :key="p.code" class="dashboard-review-row">
                <span class="record-badge" :class="badge(p.status)">{{ p.status }}</span>
                <small>{{ p.availableSlots }} left</small>
                <strong>{{ p.packageName }}</strong>
                <p>{{ p.destination }}</p>
              </div>
            </div>
            <div v-else class="empty-state">
              <h4>Stock looks healthy</h4>
              <p>No packages are running low on slots right now.</p>
            </div>

            <div class="attention-summary">
              <span class="section-label">Awaiting payment</span>
              <div class="attention-summary-figure">
                <strong>{{ peso(data.pendingPayments.amount) }}</strong>
                <span>{{ pendingCount }} unpaid {{ pendingCount === 1 ? 'booking' : 'bookings' }}</span>
              </div>
            </div>
          </section>
        </div>

        <section class="forecast-panel">
          <p class="forecast-label">Demand forecast · {{ data.forecast.method }}</p>
          <p class="forecast-headline">{{ trajectorySentence }}</p>
          <p class="forecast-detail">
            {{ data.forecast.accuracy }}% accuracy on past months · avg error {{ data.forecast.mape.toFixed(1) }}% ·
            next month ≈ {{ peso(data.forecast.nextValue) }}
          </p>
        </section>

        <p class="dash-foot">Updated {{ data.lastUpdated }}</p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type DashboardData } from '../api';

const data = ref<DashboardData | null>(null);
const loading = ref(true);
const error = ref('');

const lowCount = computed(() => data.value?.lowStockPackages.length ?? 0);
const pendingCount = computed(() => data.value?.pendingPayments.count ?? 0);
const needsAttention = computed(() => lowCount.value > 0 || pendingCount.value > 0);

const reviewSentence = computed(() => {
  if (!data.value) return '';
  const low = lowCount.value;
  const pending = pendingCount.value;
  if (low === 0 && pending === 0)
    return 'Everything looks in order — no low-stock packages or unpaid bookings.';
  const parts: string[] = [];
  if (low > 0) parts.push(`${low} ${low === 1 ? 'package is' : 'packages are'} running low on slots`);
  if (pending > 0) parts.push(`${pending} ${pending === 1 ? 'booking is' : 'bookings are'} awaiting payment`);
  return `${parts.join(' and ')} — worth a look.`;
});

const trajectorySentence = computed(() => {
  if (!data.value) return '';
  const { direction, peakMonth } = data.value.forecast;
  if (direction === 'up') return `Demand is trending upward, peaking in ${peakMonth}.`;
  if (direction === 'down') return `Demand is easing off, with the high point in ${peakMonth}.`;
  return `Demand looks steady, peaking in ${peakMonth}.`;
});

function peso(value: number): string {
  return '₱' + Math.round(value).toLocaleString('en-PH');
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
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
.review-callout {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-5);
  flex-wrap: wrap;
  border: 1px solid var(--tone-primary-border);
  border-radius: var(--radius-lg);
  background: var(--tone-primary-surface);
  padding: var(--space-5) var(--space-6);
}
.review-callout-warning {
  border-color: var(--tone-warning-border);
  background: var(--tone-warning-surface);
}
.review-callout-eyebrow {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  margin-bottom: var(--space-2);
}
.review-callout-warning .review-callout-eyebrow {
  color: var(--tone-warning-ink);
}
.review-callout-line {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 20px;
  font-weight: 540;
  letter-spacing: -0.005em;
  line-height: 1.35;
  color: var(--color-text-primary);
  max-width: 64ch;
}
.review-callout-actions {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  flex: none;
  padding-top: 2px;
}

.attention-sublabel {
  display: block;
  margin-bottom: var(--space-2);
}
.attention-summary {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin-top: var(--space-5);
  padding-top: var(--space-4);
  border-top: 1px solid var(--color-border-subtle);
}
.attention-summary-figure {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
}
.attention-summary-figure strong {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 24px;
  font-weight: 560;
  letter-spacing: -0.01em;
  color: var(--color-text-primary);
  font-variant-numeric: lining-nums tabular-nums;
}
.attention-summary-figure span {
  color: var(--color-text-muted);
  font-size: 12.5px;
}

.forecast-panel {
  padding: 1.75rem 2rem;
  background: var(--color-shell);
  color: var(--color-shell-text);
  border-radius: var(--radius-xl);
}
.forecast-label {
  margin: 0;
  font-size: 0.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-shell-muted);
}
.forecast-headline {
  margin: 0.4rem 0 0.35rem;
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 1.55rem;
  font-weight: 560;
  line-height: 1.3;
  max-width: 30ch;
}
.forecast-detail {
  margin: 0;
  color: var(--color-shell-muted);
  font-size: 0.95rem;
  font-variant-numeric: lining-nums tabular-nums;
}
.dash-note {
  margin: 1.5rem 0;
  color: var(--color-text-secondary);
}
.dash-error {
  color: var(--color-danger-ink);
}
.dash-foot {
  font-size: 0.8rem;
  color: var(--color-text-faint);
}
</style>
