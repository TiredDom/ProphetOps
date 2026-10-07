<template>
  <AppShell title="Forecast" description="Monthly revenue estimates and model results.">
    <section class="dss-page">
      <!-- Loading Skeleton State -->
      <div v-if="loading" class="forecast-loading-grid" aria-busy="true" aria-label="Loading forecast data">
        <section class="forecast-skeleton-brief">
          <div class="skeleton-line sm w-25"></div>
          <div class="skeleton-line lg w-60"></div>
          <div class="skeleton-line md w-80"></div>
          <div class="skeleton-line sm w-40"></div>
        </section>
        <section class="content-panel">
          <div class="skeleton-line md w-40"></div>
          <div class="skeleton-box chart-skeleton-lg"></div>
        </section>
      </div>

      <!-- Error State -->
      <section v-else-if="error" class="forecast-error-panel" role="alert">
        <div class="forecast-error-content">
          <h3 class="forecast-error-title">Could not load revenue forecast</h3>
          <p class="forecast-error-message">{{ error }}</p>
        </div>
        <button type="button" class="btn btn-secondary btn-sm" @click="loadForecast">
          Retry
        </button>
      </section>

      <!-- Loaded Forecast Content -->
      <template v-else-if="data">
        <!-- Active Forecast View (Rendered strictly when data.ok is true) -->
        <template v-if="data.ok">
          <!-- Back to Dashboard Navigation Link -->
          <div class="forecast-page-nav">
            <RouterLink to="/dashboard" class="compact-action-link">
              <span aria-hidden="true">←</span> Back to dashboard
            </RouterLink>
          </div>

          <!-- 1. Detailed Revenue Forecast Chart Tool (Shared Component, Recommendations Omitted) -->
          <ForecastOverviewCard
            :data="data"
            :show-recommendations="false"
            :show-forecast-link="false"
            title="Revenue forecast"
          />

          <!-- 2. Visible Monthly Breakdown Table with Prediction Ranges and Month-to-Month Changes -->
          <section class="content-panel step-panel breakdown-panel" aria-labelledby="breakdown-title">
            <div class="section-heading">
              <h2 id="breakdown-title">Monthly breakdown</h2>
              <p v-if="breakdownRange">{{ breakdownRange }}</p>
            </div>

            <table class="breakdown-table">
              <thead>
                <tr>
                  <th scope="col">Month</th>
                  <th scope="col">Forecast revenue</th>
                  <th class="desktop-bound" scope="col">Lower estimate</th>
                  <th class="desktop-bound" scope="col">Upper estimate</th>
                  <th scope="col">
                    Change
                    <span class="mobile-subnote">from prior month</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in monthlyRows" :key="row.step">
                  <td>
                    <span>{{ row.monthLabel }}</span>
                    <span v-if="row.isPeak" class="month-note">Highest forecast</span>
                  </td>
                  <td class="amount">
                    {{ peso(row.value) }}
                    <span class="mobile-range">
                      {{ formatCompactPeso(row.lower) }} – {{ formatCompactPeso(row.upper) }}
                    </span>
                  </td>
                  <td class="desktop-bound">{{ peso(row.lower) }}</td>
                  <td class="desktop-bound">{{ peso(row.upper) }}</td>
                  <td class="change" :class="row.changeDirection">
                    <template v-if="row.changePercent != null">
                      <span aria-hidden="true">{{ row.changeDirection === 'up' ? '▲' : row.changeDirection === 'down' ? '▼' : '—' }}</span>
                      {{ row.changePercent > 0 ? '+' : '' }}{{ row.changePercent.toFixed(1) }}%
                    </template>
                    <template v-else>—</template>
                  </td>
                </tr>
              </tbody>
            </table>

            <p class="table-note">
              Lower and upper estimates form the approximate 80% prediction range. Change compares with the prior forecast month; {{ monthlyRows[0]?.monthLabel }} compares with {{ lastRecordedMonthName }}'s recorded revenue.
            </p>
          </section>

          <!-- 3. Compact Model Results & Accuracy Validation Section -->
          <section class="content-panel model-panel" aria-labelledby="model-title">
            <div class="section-heading">
              <h2 id="model-title">Model results</h2>
              <p>Holt-Winters triple exponential smoothing</p>
            </div>

            <div class="model-grid">
              <!-- Left Column: Forecast settings -->
              <div class="model-col">
                <h3 class="model-col-title">Forecast settings</h3>
                <dl class="model-dl">
                  <div class="model-row">
                    <dt>Method</dt>
                    <dd>{{ data.method || 'Holt-Winters' }}</dd>
                  </div>
                  <div class="model-row">
                    <dt>Forecast period</dt>
                    <dd>{{ data.horizon || 6 }} months</dd>
                  </div>
                  <div class="model-row">
                    <dt>Seasonal cycle</dt>
                    <dd>{{ data.seasonLength || 12 }} months</dd>
                  </div>
                  <div class="model-row">
                    <dt>Measure</dt>
                    <dd>Booking revenue (PHP)</dd>
                  </div>
                  <div v-if="data.params" class="model-row">
                    <dt>Smoothing parameters</dt>
                    <dd>
                      α: {{ data.params.alpha?.toFixed(3) ?? 'auto' }},
                      β: {{ data.params.beta?.toFixed(3) ?? 'auto' }},
                      γ: {{ data.params.gamma?.toFixed(3) ?? 'auto' }}
                    </dd>
                  </div>
                </dl>
              </div>

              <!-- Right Column: Forecast accuracy & validation -->
              <div class="model-col validation-col">
                <h3 class="model-col-title">Forecast accuracy</h3>
                <dl class="model-dl">
                  <div class="model-row">
                    <dt>Model accuracy</dt>
                    <dd><strong>{{ data.accuracy }}%</strong></dd>
                  </div>
                  <div class="model-row">
                    <dt>Mean Absolute Pct Error (MAPE)</dt>
                    <dd>{{ data.metrics?.mape != null ? data.metrics.mape.toFixed(1) + '%' : 'N/A' }}</dd>
                  </div>
                  <div class="model-row">
                    <dt>Mean Absolute Error (MAE)</dt>
                    <dd>{{ peso(data.metrics?.mae ?? 0) }}</dd>
                  </div>
                  <div class="model-row">
                    <dt>Root Mean Squared Error (RMSE)</dt>
                    <dd>{{ peso(data.metrics?.rmse ?? 0) }}</dd>
                  </div>
                  <div class="model-row">
                    <dt>Evaluation sample</dt>
                    <dd>{{ data.metrics?.sampleSize ?? 0 }} months</dd>
                  </div>
                  <div v-if="data.baselines?.seasonalNaiveMae != null" class="model-row">
                    <dt>Seasonal Naive MAE baseline</dt>
                    <dd>{{ peso(data.baselines.seasonalNaiveMae) }}</dd>
                  </div>
                </dl>
              </div>
            </div>

            <!-- Calculation details disclosure -->
            <details class="calculation-details">
              <summary>Calculation details</summary>
              <div class="calculation-details-body">
                <p>
                  Holt-Winters combines the revenue level, trend, and seasonal pattern. The prediction range expresses uncertainty; it does not guarantee the amount will be earned.
                </p>
                <p>
                  The measure is recorded booking revenue in Philippine Pesos (₱), not profit, cash received, or travel demand. Expense records remain separate from future revenue estimates.
                </p>
              </div>
            </details>
          </section>
        </template>

        <!-- Insufficient Data State: Compact Full-Width Status Layout -->
        <section v-else class="content-panel insufficient-full-panel" aria-label="Insufficient history status">
          <div class="insufficient-status-grid">
            <div class="insufficient-main">
              <h3 class="insufficient-status-title">
                Not enough history for a revenue forecast
              </h3>
              <p class="insufficient-status-desc">
                Live forecasting begins automatically once two annual cycles ({{ data.dataSource.minimumMonths }} months) are recorded to establish seasonality and trend. Predicts recorded booking revenue in ₱, not passenger volume or travel demand.
              </p>
            </div>

            <div class="insufficient-side">
              <div class="insufficient-stat-block">
                <span class="insufficient-stat-figure">
                  <strong>{{ data.dataSource.recordedMonths }} of {{ data.dataSource.minimumMonths }}</strong> recorded months
                </span>
                <span v-if="fullMonthsNeeded > 0" class="insufficient-stat-sub">
                  {{ fullMonthsNeeded }} more {{ fullMonthsNeeded === 1 ? 'month' : 'months' }} needed
                </span>
              </div>
              <RouterLink to="/bookings" class="compact-action-link">
                View booking records <span aria-hidden="true">→</span>
              </RouterLink>
            </div>
          </div>

          <details class="insufficient-how-it-works">
            <summary>How forecasting works</summary>
            <div class="how-it-works-body">
              <p>
                Holt-Winters triple exponential smoothing requires two complete annual cycles (24 months) of booking records to separate seasonal patterns from baseline trends. The model operates strictly on recorded booking revenue in Philippine Pesos (₱), rather than passenger volume or travel demand.
              </p>
              <RouterLink to="/reports" class="compact-action-link">
                View existing reports <span aria-hidden="true">→</span>
              </RouterLink>
            </div>
          </details>
        </section>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import ForecastOverviewCard from '../components/ForecastOverviewCard.vue';
import { api, type ForecastData } from '../api';
import { formatCompactPeso } from '../forecastChart';
import { peso } from '../format';

const data = ref<ForecastData | null>(null);
const loading = ref(true);
const error = ref('');

interface MonthlyBreakdownRow {
  step: number;
  month: string;
  monthLabel: string;
  value: number;
  lower: number;
  upper: number;
  isPeak: boolean;
  changePercent: number | null;
  changeDirection: 'up' | 'down' | 'flat';
}

const monthlyRows = computed<MonthlyBreakdownRow[]>(() => {
  const d = data.value;
  if (!d?.ok || !d.steps?.length) return [];
  const steps = d.steps;
  const history = d.history ?? [];
  const lastHistory = history.length > 0 ? history[history.length - 1] : null;
  const peakVal = Math.max(...steps.map((s) => s.value));

  return steps.map((s, idx) => {
    let prevVal: number | null = null;
    if (idx === 0) {
      if (lastHistory && Number.isFinite(lastHistory.value) && lastHistory.value > 0) {
        prevVal = lastHistory.value;
      }
    } else {
      prevVal = steps[idx - 1].value;
    }

    let changePercent: number | null = null;
    let changeDirection: 'up' | 'down' | 'flat' = 'flat';
    if (prevVal != null && prevVal > 0) {
      changePercent = ((s.value - prevVal) / prevVal) * 100;
      if (Math.abs(changePercent) < 0.05) {
        changeDirection = 'flat';
      } else if (changePercent > 0) {
        changeDirection = 'up';
      } else {
        changeDirection = 'down';
      }
    }

    return {
      step: s.step,
      month: s.month,
      monthLabel: s.monthLabel,
      value: s.value,
      lower: s.lower,
      upper: s.upper,
      isPeak: Math.abs(s.value - peakVal) < 1e-4,
      changePercent,
      changeDirection,
    };
  });
});

const breakdownRange = computed(() => {
  const rows = monthlyRows.value;
  if (!rows.length) return '';
  return `${rows[0].monthLabel} – ${rows[rows.length - 1].monthLabel}`;
});

const lastRecordedMonthName = computed(() => {
  const d = data.value;
  if (!d) return 'prior month';
  return d.dataSource.lastRecordedMonth ?? (d.history?.length ? d.history[d.history.length - 1].label : 'prior month');
});

const fullMonthsNeeded = computed(() => {
  const s = data.value?.dataSource;
  if (!s) return 24;
  return Math.max(0, s.minimumMonths - s.recordedMonths);
});

async function loadForecast() {
  loading.value = true;
  error.value = '';
  try {
    data.value = await api.forecast();
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Could not load the forecast.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  loadForecast();
});
</script>

<style scoped>
.forecast-page-nav {
  margin-bottom: 12px;
}

.compact-action-link {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--color-primary);
  font-size: 13.5px;
  font-weight: 600;
  text-decoration: none;
}

.compact-action-link:hover {
  color: var(--color-primary-hover);
  text-decoration: underline;
  text-underline-offset: 3px;
}

/* ============================================================
   Monthly Breakdown Table (Matches forecast-detail-draft.html)
   ============================================================ */
.breakdown-panel {
  margin-bottom: 24px;
}

.section-heading {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 14px;
}

.section-heading h2 {
  margin: 0;
  font-size: 18.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.section-heading p {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-muted);
}

.breakdown-table {
  width: 100%;
  border-collapse: collapse;
  font-variant-numeric: tabular-nums;
}

.breakdown-table th {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-muted);
  background: #eef1f4;
  border-bottom: 1px solid var(--color-border);
}

.breakdown-table th,
.breakdown-table td {
  padding: 13px 14px;
  border-bottom: 1px solid var(--color-border-subtle);
  font-size: 13.5px;
  text-align: right;
}

.breakdown-table th:first-child,
.breakdown-table td:first-child {
  text-align: left;
}

.month-note {
  display: block;
  color: #13796c;
  font-size: 11.5px;
  font-weight: 600;
  margin-top: 2px;
  letter-spacing: 0;
}

.amount {
  font-weight: 600;
  color: var(--color-text-primary);
}

.mobile-range {
  display: none;
}

.mobile-subnote {
  display: none;
}

.change {
  font-weight: 600;
  white-space: nowrap;
}

.change.up {
  color: #126c5f;
}

.change.down {
  color: #a33d41;
}

.change.flat {
  color: var(--color-text-muted);
}

.table-note {
  margin: 12px 0 0;
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.5;
}

/* ============================================================
   Model Results & Accuracy (Matches forecast-detail-draft.html)
   ============================================================ */
.model-panel {
  margin-bottom: 24px;
}

.model-grid {
  display: grid;
  gap: 24px;
  margin-top: 14px;
}

.model-col-title {
  margin: 0 0 10px;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.model-dl {
  margin: 0;
}

.model-row {
  display: flex;
  gap: 20px;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid var(--color-border-subtle);
  font-size: 13.5px;
}

.model-row dt {
  color: var(--color-text-muted);
  font-weight: 400;
}

.model-row dd {
  margin: 0;
  text-align: right;
  font-weight: 600;
  color: var(--color-text-primary);
  font-variant-numeric: lining-nums tabular-nums;
}

.calculation-details {
  margin-top: 20px;
  border-top: 1px solid var(--color-border-subtle);
  padding-top: 10px;
}

.calculation-details summary {
  cursor: pointer;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--color-primary);
  padding: 6px 0;
  outline: none;
}

.calculation-details summary:hover {
  color: var(--color-primary-hover);
}

.calculation-details-body {
  padding-top: 8px;
}

.calculation-details-body p {
  margin: 6px 0;
  max-width: 70ch;
  font-size: 13px;
  line-height: 1.5;
  color: var(--color-text-secondary);
}

/* ============================================================
   Insufficient Data State
   ============================================================ */
.insufficient-full-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-bottom: 24px;
}

.insufficient-status-grid {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: var(--space-5);
  flex-wrap: wrap;
}

.insufficient-main {
  flex: 1 1 450px;
}

.insufficient-status-title {
  margin: 0 0 6px;
  font-size: 17px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.insufficient-status-desc {
  margin: 0;
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--color-text-secondary);
  max-width: 65ch;
}

.insufficient-side {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 10px;
  padding-left: var(--space-4);
  border-left: 2px solid var(--color-border-subtle);
}

.insufficient-stat-block {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.insufficient-stat-figure {
  font-size: 14.5px;
  color: var(--color-text-primary);
}

.insufficient-stat-figure strong {
  font-weight: 700;
  font-variant-numeric: lining-nums tabular-nums;
}

.insufficient-stat-sub {
  font-size: 12px;
  color: var(--color-text-muted);
}

.insufficient-how-it-works {
  border-top: 1px solid var(--color-border-subtle);
  padding-top: 12px;
}

.insufficient-how-it-works summary {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
  cursor: pointer;
  padding: 4px 0;
}

.how-it-works-body {
  padding-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.how-it-works-body p {
  margin: 0;
  font-size: 13px;
  line-height: 1.5;
  color: var(--color-text-secondary);
}

/* ============================================================
   Loading & Skeletons
   ============================================================ */
.forecast-loading-grid {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  margin-bottom: var(--space-5);
}

.forecast-skeleton-brief {
  padding: 20px 24px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.chart-skeleton-lg {
  height: 280px;
  margin-top: 14px;
}

/* Error State */
.forecast-error-panel {
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

.forecast-error-title {
  margin: 0 0 4px;
  font-size: 14.5px;
  font-weight: 600;
  color: var(--color-danger);
}

.forecast-error-message {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-secondary);
}

/* ============================================================
   Responsive Media Queries
   ============================================================ */
@media (min-width: 760px) {
  .model-grid {
    grid-template-columns: 1fr 1fr;
    gap: 40px;
  }
}

@media (max-width: 620px) {
  .breakdown-table th,
  .breakdown-table td {
    padding: 12px 9px;
  }

  .desktop-bound {
    display: none !important;
  }

  .mobile-range {
    display: block;
    font-size: 12px;
    font-weight: 400;
    color: var(--color-text-muted);
    margin-top: 4px;
    white-space: normal;
  }

  .mobile-subnote {
    display: block;
    font-size: 10.5px;
    font-weight: 400;
    color: var(--color-text-muted);
  }

  .change {
    font-size: 12px;
  }

  .table-note {
    line-height: 1.6;
  }

  .insufficient-side {
    padding-left: 0;
    border-left: none;
    padding-top: 12px;
    border-top: 1px solid var(--color-border-subtle);
    width: 100%;
  }
}
</style>
