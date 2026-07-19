<template>
  <AppShell title="Reports" description="A one-page summary of the money and the records behind it.">
    <section class="dss-page">
      <div v-if="loading" class="content-panel">
        <Skeleton :rows="6" />
      </div>

      <div v-else-if="error" class="content-panel report-error" role="alert">
        <p class="report-error-title">We could not load the summary</p>
        <p class="report-error-text">{{ error }}</p>
      </div>

      <template v-else-if="data">
        <section class="content-panel report-hero" aria-label="Money at a glance">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Money at a glance</h2>
            </div>
            <span class="panel-meta">All bookings and recorded costs</span>
          </div>

          <div class="report-money">
            <div class="report-money-figure">
              <span class="report-money-label">Revenue</span>
              <strong class="report-money-value">{{ peso(data.revenue) }}</strong>
              <span class="report-money-note">Money coming in</span>
            </div>
            <div class="report-money-figure">
              <span class="report-money-label">Costs</span>
              <strong class="report-money-value">{{ peso(data.costs) }}</strong>
              <span class="report-money-note">Money going out</span>
            </div>
            <div class="report-money-figure">
              <span class="report-money-label">Profit</span>
              <strong class="report-money-value" :class="isLoss ? 'is-loss' : 'is-gain'">{{ peso(data.profit) }}</strong>
              <span class="report-money-note">{{ isLoss ? 'Costs are above revenue' : 'What is left over' }}</span>
            </div>
          </div>

          <div class="report-split" aria-hidden="true">
            <div class="report-split-track">
              <i class="report-split-seg seg-costs" :style="{ width: costsPct + '%' }"></i>
              <i class="report-split-seg seg-profit" :style="{ width: profitPct + '%' }"></i>
            </div>
          </div>
          <div class="report-split-legend">
            <span class="report-split-key">
              <i class="report-key-dot dot-costs"></i>
              Costs take {{ costsPct }}% of revenue
            </span>
            <span class="report-split-key">
              <i class="report-key-dot dot-profit"></i>
              {{ isLoss ? 'No profit left' : 'Profit is ' + profitPct + '% of revenue' }}
            </span>
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

        <div class="dss-grid two-column">
          <section class="content-panel">
            <div class="panel-header">
              <div class="panel-title-group">
                <h2>Revenue by package</h2>
              </div>
              <span class="panel-meta">Where the money comes from</span>
            </div>

            <ul v-if="data.revenueByPackage.length" class="report-bars tone-primary">
              <li v-for="row in data.revenueByPackage" :key="row.label" class="report-bar-row">
                <span class="report-bar-label">{{ row.label }}</span>
                <span class="report-bar-track"><i :style="{ width: barWidth(row.value, packageMax) }"></i></span>
                <strong class="report-bar-value">{{ peso(row.value) }}</strong>
              </li>
            </ul>
            <EmptyState
              v-else
              title="No package revenue yet"
              message="Paid bookings will show up here once they are recorded."
            />
          </section>

          <div class="report-column">
            <section class="content-panel">
              <div class="panel-header">
                <div class="panel-title-group">
                  <h2>Bookings by status</h2>
                </div>
                <span class="panel-meta">How many are in each stage</span>
              </div>

              <ul v-if="data.bookingsByStatus.length" class="report-bars tone-data">
                <li v-for="row in data.bookingsByStatus" :key="row.label" class="report-bar-row">
                  <span class="report-bar-label">
                    <span class="record-badge" :class="badge(row.label)">{{ row.label }}</span>
                  </span>
                  <span class="report-bar-track"><i :style="{ width: barWidth(row.value, statusMax) }"></i></span>
                  <strong class="report-bar-value">{{ row.value }}</strong>
                </li>
              </ul>
              <EmptyState
                v-else
                title="No bookings yet"
                message="New bookings will be grouped here by their stage."
              />
            </section>

            <section class="content-panel">
              <div class="panel-header">
                <div class="panel-title-group">
                  <h2>Expenses by category</h2>
                </div>
                <span class="panel-meta">Where the money goes</span>
              </div>

              <ul v-if="data.expensesByCategory.length" class="report-bars tone-accent">
                <li v-for="row in data.expensesByCategory" :key="row.label" class="report-bar-row">
                  <span class="report-bar-label">{{ row.label }}</span>
                  <span class="report-bar-track"><i :style="{ width: barWidth(row.value, expenseMax) }"></i></span>
                  <strong class="report-bar-value">{{ peso(row.value) }}</strong>
                </li>
              </ul>
              <EmptyState
                v-else
                title="No expenses yet"
                message="Recorded costs will be grouped by category here."
              />
            </section>
          </div>
        </div>

        <p class="report-foot">Generated {{ data.generatedAt }}</p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import { api, type ReportsData } from '../api';
import { peso } from '../format';

const data = ref<ReportsData | null>(null);
const loading = ref(true);
const error = ref('');


function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function barWidth(value: number, max: number): string {
  if (max <= 0) return '2px';
  const pct = Math.max((value / max) * 100, 1.5);
  return Math.min(pct, 100) + '%';
}

const packageMax = computed(() =>
  Math.max(0, ...(data.value?.revenueByPackage.map((r) => r.value) ?? [])),
);
const statusMax = computed(() =>
  Math.max(0, ...(data.value?.bookingsByStatus.map((r) => r.value) ?? [])),
);
const expenseMax = computed(() =>
  Math.max(0, ...(data.value?.expensesByCategory.map((r) => r.value) ?? [])),
);

const isLoss = computed(() => (data.value?.profit ?? 0) < 0);

const costsPct = computed(() => {
  const d = data.value;
  if (!d || d.revenue <= 0) return 0;
  return Math.round(Math.min((d.costs / d.revenue) * 100, 100));
});

const profitPct = computed(() => {
  const d = data.value;
  if (!d || d.revenue <= 0) return 0;
  return Math.round(Math.max(Math.min((d.profit / d.revenue) * 100, 100), 0));
});

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
.report-hero {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.report-money {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.report-money-figure {
  display: flex;
  flex-direction: column;
  gap: 7px;
  padding: 4px 24px;
  min-width: 0;
  border-left: 1px solid var(--color-border-subtle);
}

.report-money-figure:first-child {
  border-left: none;
  padding-left: 0;
}

.report-money-label {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.report-money-value {
  font-family: var(--font-display);
  font-optical-sizing: auto;
  font-size: 34px;
  font-weight: 580;
  letter-spacing: -0.015em;
  line-height: 1.1;
  color: var(--color-text-primary);
  overflow-wrap: anywhere;
  font-variant-numeric: lining-nums tabular-nums;
}

.report-money-value.is-gain {
  color: var(--color-success-ink);
}

.report-money-value.is-loss {
  color: var(--color-danger-ink);
}

.report-money-note {
  color: var(--color-text-muted);
  font-size: 12.5px;
}

.report-split-track {
  display: flex;
  width: 100%;
  height: 14px;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
  overflow: hidden;
}

.report-split-seg {
  display: block;
  height: 100%;
  min-width: 0;
}

.report-split-seg.seg-costs {
  background: var(--color-accent);
}

.report-split-seg.seg-profit {
  background: var(--color-primary);
}

.report-split-legend {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3) var(--space-6);
  margin-top: calc(var(--space-3) * -1);
  color: var(--color-text-secondary);
  font-size: 13px;
}

.report-split-key {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.report-key-dot {
  width: 9px;
  height: 9px;
  border-radius: 2px;
  flex: none;
}

.report-key-dot.dot-costs {
  background: var(--color-accent);
}

.report-key-dot.dot-profit {
  background: var(--color-primary);
}

.report-column {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  min-width: 0;
}

.report-bars {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  list-style: none;
}

.report-bar-row {
  display: grid;
  grid-template-columns: minmax(0, 8.5rem) minmax(0, 1fr) auto;
  align-items: center;
  gap: 14px;
}

.report-bar-label {
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 500;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.report-bar-track {
  position: relative;
  height: 9px;
  background: var(--color-surface-sunken);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.report-bar-track i {
  position: absolute;
  inset: 0 auto 0 0;
  display: block;
  min-width: 2px;
  border-radius: var(--radius-pill);
  background: var(--color-text-faint);
}

.report-bars.tone-primary .report-bar-track i {
  background: var(--color-primary);
}

.report-bars.tone-accent .report-bar-track i {
  background: var(--color-accent);
}

.report-bars.tone-data .report-bar-track i {
  background: var(--color-data);
}

.report-bar-value {
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
  text-align: right;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.report-error {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.report-error-title {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 600;
}

.report-error-text {
  color: var(--color-danger-ink);
  font-size: 13px;
}

.report-foot {
  margin-top: 2px;
  color: var(--color-text-muted);
  font-size: 12.5px;
}

@media (max-width: 640px) {
  .report-money {
    grid-template-columns: 1fr;
    gap: var(--space-2);
  }

  .report-money-figure {
    padding: 12px 0;
    border-left: none;
    border-top: 1px solid var(--color-border-subtle);
  }

  .report-money-figure:first-child {
    border-top: none;
    padding-top: 0;
  }

  .report-money-value {
    font-size: 29px;
  }

  .report-bar-row {
    grid-template-columns: minmax(0, 6.5rem) minmax(0, 1fr) auto;
    gap: 10px;
  }
}
</style>
