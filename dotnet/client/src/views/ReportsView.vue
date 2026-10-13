<template>
  <AppShell title="Reports">
    <template #actions>
      <button class="secondary-button" type="button" :disabled="!data" @click="printReport">
        <UiIcon name="printer" />
        Print
      </button>
      <a class="secondary-button" :href="exportHref" title="Download the month-by-month figures for this period as a CSV file">
        <UiIcon name="download" />
        Export CSV
      </a>
    </template>

    <section class="dss-page report-page">
      <p class="print-heading">
        ProphetOps · Renan-Tina Travels &amp; Tours · Report for {{ periodLabel }}
      </p>

      <div class="content-panel report-period">
        <PeriodSelect
          v-model:period="periodKey"
          v-model:from="filters.from"
          v-model:to="filters.to"
          :today="today"
        />
        <p v-if="data" class="report-period-text" aria-live="polite">
          <strong>{{ periodLabel }}</strong>
          <template v-if="previousLabel">, compared with <strong>{{ previousLabel }}</strong></template>
          <template v-else-if="data.totalsScope === 'lifetime'"> · every recorded month</template>
        </p>
      </div>

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="8" />
      </div>

      <div v-else-if="error" class="content-panel report-error" role="alert">
        <p class="report-error-title">The report could not be loaded</p>
        <p class="report-error-text">{{ error }}</p>
        <button class="secondary-button" type="button" @click="load">Try again</button>
      </div>

      <template v-else-if="data">
        <!-- 1. The period in one table, against the period before it -->
        <section class="content-panel" aria-labelledby="summary-title">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2 id="summary-title">Summary</h2>
            </div>
            <span v-if="headline" class="panel-meta report-headline">{{ headline }}</span>
          </div>

          <div class="table-scroll">
            <table class="dss-table statement-table">
              <thead>
                <tr>
                  <th scope="col"><span class="visually-hidden">Measure</span></th>
                  <th scope="col" class="num">
                    <span class="figure-full">{{ data.previous ? shortRange(data.period.from, data.period.through) : 'Total' }}</span>
                    <span class="figure-short">{{ data.previous ? 'This period' : 'Total' }}</span>
                  </th>
                  <th v-if="data.previous" scope="col" class="num previous-col">{{ shortRange(data.previous.from, data.previous.to) }}</th>
                  <th v-if="data.previous" scope="col" class="num">Change</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in statement" :key="row.label" :class="{ 'is-total': row.total }">
                  <th scope="row">
                    {{ row.label }}
                    <span v-if="row.note" class="row-subtext">{{ row.note }}</span>
                  </th>
                  <td class="num" :class="{ 'is-loss': row.loss }"><strong>{{ row.current }}</strong></td>
                  <td v-if="data.previous" class="num previous-col">{{ row.previous }}</td>
                  <td v-if="data.previous" class="num">
                    <span class="change" :class="row.change.tone">
                      <UiIcon v-if="row.change.icon" :name="row.change.icon" class="change-icon" />
                      {{ row.change.text }}
                    </span>
                    <span class="row-subtext previous-inline">from {{ row.previousShort }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- 2. Month by month -->
        <section class="content-panel" aria-labelledby="months-title">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2 id="months-title">Month by month</h2>
            </div>
            <span class="panel-meta chart-key" aria-hidden="true">
              <span><i class="key-revenue"></i>Revenue</span>
              <span><i class="key-costs"></i>Costs</span>
            </span>
          </div>

          <div v-if="data.monthly.length" ref="chartBox" class="month-chart-box">
            <svg
              class="month-chart"
              :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
              role="img"
              :aria-label="chartLabel"
            >
              <g v-for="line in gridLines" :key="line.value">
                <line class="grid-line" :class="{ base: line.value === 0 }" :x1="plotLeft" :x2="plotRight" :y1="line.y" :y2="line.y" />
                <text class="axis-label" :x="plotLeft - 8" :y="line.y" text-anchor="end" dominant-baseline="middle">{{ line.label }}</text>
              </g>
              <g v-for="bar in bars" :key="bar.month">
                <rect class="bar-revenue" :x="bar.x" :y="bar.revenueY" :width="bar.width" :height="bar.revenueHeight" rx="2">
                  <title>{{ bar.label }} revenue {{ peso(bar.revenue) }}</title>
                </rect>
                <rect class="bar-costs" :x="bar.x + bar.width + 2" :y="bar.costsY" :width="bar.width" :height="bar.costsHeight" rx="2">
                  <title>{{ bar.label }} costs {{ peso(bar.costs) }}</title>
                </rect>
                <text v-if="bar.showLabel" class="axis-label" :x="bar.center" :y="plotBottom + 18" text-anchor="middle">{{ bar.short }}</text>
              </g>
            </svg>
          </div>

          <div v-if="data.monthly.length" class="table-scroll">
            <table class="dss-table month-table">
              <thead>
                <tr>
                  <th scope="col">Month</th>
                  <th scope="col" class="num count-col">Bookings</th>
                  <th scope="col" class="num count-col">Passengers</th>
                  <th scope="col" class="num">Revenue</th>
                  <th scope="col" class="num">Costs</th>
                  <th scope="col" class="num">After costs</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="m in data.monthly" :key="m.month">
                  <th scope="row">{{ m.label }}</th>
                  <td class="num count-col">{{ count(m.bookings) }}</td>
                  <td class="num count-col">{{ count(m.passengers) }}</td>
                  <td class="num"><span class="figure-full">{{ peso(m.revenue) }}</span><span class="figure-short">{{ pesoCompact(m.revenue) }}</span></td>
                  <td class="num"><span class="figure-full">{{ peso(m.costs) }}</span><span class="figure-short">{{ pesoCompact(m.costs) }}</span></td>
                  <td class="num" :class="{ 'is-loss': m.afterCosts < 0 }"><span class="figure-full">{{ peso(m.afterCosts) }}</span><span class="figure-short">{{ pesoCompact(m.afterCosts) }}</span></td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <th scope="row">Total</th>
                  <td class="num count-col">{{ count(data.counts.bookings) }}</td>
                  <td class="num count-col">{{ count(data.counts.passengers) }}</td>
                  <td class="num"><span class="figure-full">{{ peso(data.revenue) }}</span><span class="figure-short">{{ pesoCompact(data.revenue) }}</span></td>
                  <td class="num"><span class="figure-full">{{ peso(data.costs) }}</span><span class="figure-short">{{ pesoCompact(data.costs) }}</span></td>
                  <td class="num" :class="{ 'is-loss': data.profit < 0 }"><span class="figure-full">{{ peso(data.profit) }}</span><span class="figure-short">{{ pesoCompact(data.profit) }}</span></td>
                </tr>
              </tfoot>
            </table>
          </div>

          <p v-else class="report-empty">Nothing is recorded in this period.</p>
        </section>

        <!-- 3. Where it came from and where it went: revenue on the left, spending and bookings on the right -->
        <div class="report-grid">
          <div v-for="(column, index) in breakdownColumns" :key="index" class="report-column">
          <section v-for="panel in column" :key="panel.title" class="content-panel" :aria-label="panel.title">
            <div class="panel-header">
              <div class="panel-title-group">
                <h2>{{ panel.title }}</h2>
              </div>
              <span class="panel-meta">{{ panel.meta }}</span>
            </div>
            <ul v-if="panel.rows.length" class="report-bars" :class="panel.tone">
              <li v-for="row in panel.rows" :key="row.label" class="report-bar-row">
                <span class="report-bar-label" :title="row.label">{{ row.label }}</span>
                <span class="report-bar-track"><i :style="{ width: row.width }"></i></span>
                <span class="report-bar-value">
                  <strong>{{ row.value }}</strong>
                  <span v-if="row.detail" class="report-bar-detail">{{ row.detail }}</span>
                </span>
              </li>
            </ul>
            <p v-else class="report-empty">{{ panel.empty }}</p>
          </section>
          </div>
        </div>

        <p class="report-foot">
          Generated {{ data.generatedAt }}. Revenue is gross booking revenue and leaves out voided bookings; costs leave
          out voided expenses. Revenue after costs is revenue less recorded expenses, not accounting profit.
        </p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import AppShell from '../components/AppShell.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import Skeleton from '../components/Skeleton.vue';
import UiIcon, { type UiIconName } from '../components/UiIcon.vue';
import { useBusinessDate } from '../composables/useBusinessDate';
import { useContentWidth } from '../composables/useContentWidth';
import { useQueryState } from '../composables/useQueryState';
import { api, type ReportLine, type ReportsData } from '../api';
import { count, peso, pesoCompact, stateLabel } from '../format';
import { describeRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';

const data = ref<ReportsData | null>(null);
const loading = ref(true);
const error = ref('');
const { today, load: loadToday } = useBusinessDate();

// A year to date is the usual report; any period can be chosen and kept in the address.
const filters = useQueryState(
  { period: 'year', from: '', to: '' },
  { period: isPeriodKey, from: isIsoDate, to: isIsoDate },
);

const periodKey = computed<PeriodKey>({
  get: () => (isPeriodKey(filters.period) ? filters.period : 'year'),
  set: (value) => {
    filters.period = value;
    if (value !== 'custom') {
      filters.from = '';
      filters.to = '';
    }
  },
});

const range = computed(() => resolvePeriod(periodKey.value, today.value, filters.from, filters.to));

const exportHref = computed(() => {
  const params = new URLSearchParams();
  if (range.value?.from) params.set('from', range.value.from);
  if (range.value?.to) params.set('to', range.value.to);
  const query = params.toString();
  return '/api/export/report.csv' + (query ? '?' + query : '');
});

function shortRange(from: string | null, to: string | null): string {
  if (!from && !to) return 'All records';
  return describeRange({ from: from ?? '', to: to ?? '' });
}

const periodLabel = computed(() => {
  const d = data.value;
  if (!d) return range.value ? describeRange(range.value) : 'all records';
  if (d.totalsScope === 'lifetime') return 'All records';
  return shortRange(d.period.from, d.period.through);
});

const previousLabel = computed(() => {
  const p = data.value?.previous;
  return p ? shortRange(p.from, p.to) : '';
});

interface Change {
  text: string;
  tone: string;
  icon: UiIconName | null;
}

/** Change against the earlier period. Costs are shown without a good or bad colour. */
function change(current: number, previous: number, neutral = false): Change {
  if (previous === 0 && current === 0) return { text: 'No change', tone: 'flat', icon: null };
  if (previous === 0) return { text: 'New this period', tone: neutral ? 'flat' : 'up', icon: null };
  const percent = ((current - previous) / Math.abs(previous)) * 100;
  if (Math.abs(percent) < 0.05) return { text: 'No change', tone: 'flat', icon: null };
  const text = `${percent > 0 ? '+' : '−'}${Math.abs(percent).toFixed(1)}%`;
  if (neutral) return { text, tone: 'flat', icon: percent > 0 ? 'up' : 'down' };
  return { text, tone: percent > 0 ? 'up' : 'down', icon: percent > 0 ? 'up' : 'down' };
}

const statement = computed(() => {
  const d = data.value;
  if (!d) return [];
  const p = d.previous;
  return [
    { label: 'Revenue', note: 'Gross booking revenue', current: peso(d.revenue), previous: p ? peso(p.revenue) : '', previousShort: p ? pesoCompact(p.revenue) : '', change: change(d.revenue, p?.revenue ?? 0), loss: false, total: false },
    { label: 'Costs', note: 'Recorded expenses', current: peso(d.costs), previous: p ? peso(p.costs) : '', previousShort: p ? pesoCompact(p.costs) : '', change: change(d.costs, p?.costs ?? 0, true), loss: false, total: false },
    { label: 'Revenue after costs', note: '', current: peso(d.profit), previous: p ? peso(p.profit) : '', previousShort: p ? pesoCompact(p.profit) : '', change: change(d.profit, p?.profit ?? 0), loss: d.profit < 0, total: true },
    { label: 'Bookings', note: '', current: count(d.counts.bookings), previous: p ? count(p.bookings) : '', previousShort: p ? count(p.bookings) : '', change: change(d.counts.bookings, p?.bookings ?? 0), loss: false, total: false },
    { label: 'Passengers', note: '', current: count(d.counts.passengers), previous: p ? count(p.passengers) : '', previousShort: p ? count(p.passengers) : '', change: change(d.counts.passengers, p?.passengers ?? 0), loss: false, total: false },
  ];
});

/** One plain sentence about revenue against the earlier period. */
const headline = computed(() => {
  const d = data.value;
  if (!d?.previous) return '';
  const c = change(d.revenue, d.previous.revenue);
  if (c.tone === 'flat') return 'Revenue is level with the earlier period.';
  if (c.text === 'New this period') return 'Revenue is new this period; the earlier period recorded none.';
  return `Revenue is ${c.tone === 'up' ? 'up' : 'down'} ${c.text.replace(/^[+−]/, '')} on the earlier period.`;
});

// ----- Month by month chart -----

const chartBox = ref<HTMLElement | null>(null);
const { width: chartWidth, attach: attachChart } = useContentWidth(chartBox, 640);
const chartHeight = 230;
const plotLeft = 64;
const plotTop = 14;
const plotBottom = 196;
const plotRight = computed(() => chartWidth.value - 8);

function niceStep(peak: number): number {
  if (peak <= 0) return 1;
  const rough = peak / 4;
  const power = Math.pow(10, Math.floor(Math.log10(rough)));
  const norm = rough / power;
  const factor = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10;
  return factor * power;
}

const scale = computed(() => {
  const months = data.value?.monthly ?? [];
  const peak = Math.max(0, ...months.map((m) => Math.max(m.revenue, m.costs)));
  const step = niceStep(peak);
  return { step, max: Math.max(step, Math.ceil(peak / step) * step) };
});

const gridLines = computed(() => {
  const { step, max } = scale.value;
  const lines: { value: number; y: number; label: string }[] = [];
  for (let value = 0; value <= max + step / 2; value += step) {
    lines.push({ value, y: plotBottom - (value / max) * (plotBottom - plotTop), label: pesoCompact(value) });
  }
  return lines;
});

const bars = computed(() => {
  const months = data.value?.monthly ?? [];
  const band = months.length ? (plotRight.value - plotLeft) / months.length : 0;
  const width = Math.max(2, Math.min(18, (band - 6) / 2));
  const stride = band > 0 ? Math.max(1, Math.ceil(44 / band)) : 1;
  const { max } = scale.value;
  const height = (value: number) => (max > 0 ? (Math.max(0, value) / max) * (plotBottom - plotTop) : 0);
  return months.map((m, i) => {
    const center = plotLeft + band * i + band / 2;
    const [year, month] = m.month.split('-');
    const short = new Date(Date.UTC(Number(year), Number(month) - 1, 1)).toLocaleString('en-US', { month: 'short', timeZone: 'UTC' });
    return {
      month: m.month,
      label: m.label,
      short: month === '01' || i === 0 ? `${short} ${year.slice(2)}` : short,
      revenue: m.revenue,
      costs: m.costs,
      center,
      x: center - width - 1,
      width,
      revenueHeight: height(m.revenue),
      revenueY: plotBottom - height(m.revenue),
      costsHeight: height(m.costs),
      costsY: plotBottom - height(m.costs),
      showLabel: i % stride === 0,
    };
  });
});

const chartLabel = computed(() => {
  const months = data.value?.monthly ?? [];
  if (!months.length) return 'Monthly revenue and costs. Nothing recorded.';
  return 'Monthly revenue and costs, oldest first: '
    + months.map((m) => `${m.label} revenue ${peso(m.revenue)}, costs ${peso(m.costs)}`).join('; ');
});

// ----- Breakdowns -----

function widths(rows: ReportLine[], pick: (row: ReportLine) => number) {
  const max = Math.max(1, ...rows.map(pick));
  return (row: ReportLine) => `${Math.max(1.5, Math.min(100, (pick(row) / max) * 100))}%`;
}

const breakdowns = computed(() => {
  const d = data.value;
  if (!d) return [];
  const revenueWidth = widths(d.revenueByPackage, (r) => r.value);
  const destinationWidth = widths(d.revenueByDestination, (r) => r.value);
  const categoryWidth = widths(d.expensesByCategory, (r) => r.value);
  const paymentWidth = widths(d.bookingsByPayment, (r) => r.value);
  const stageWidth = widths(d.bookingsByStatus, (r) => r.value);
  const plural = (n: number, word: string) => `${count(n)} ${word}${n === 1 ? '' : 's'}`;
  return [
    {
      title: 'Revenue by package',
      meta: 'Gross booking revenue',
      tone: 'tone-primary',
      empty: 'No bookings in this period.',
      rows: d.revenueByPackage.slice(0, 10).map((r) => ({ label: r.label, value: peso(r.value), detail: plural(r.count ?? 0, 'booking'), width: revenueWidth(r) })),
    },
    {
      title: 'Spending by category',
      meta: 'Recorded expenses',
      tone: 'tone-data',
      empty: 'No expenses in this period.',
      rows: d.expensesByCategory.map((r) => ({ label: r.label, value: peso(r.value), detail: plural(r.count ?? 0, 'expense'), width: categoryWidth(r) })),
    },
    {
      title: 'Revenue by destination',
      meta: 'Gross booking revenue',
      tone: 'tone-primary',
      empty: 'No bookings in this period.',
      rows: d.revenueByDestination.slice(0, 10).map((r) => ({ label: r.label, value: peso(r.value), detail: plural(r.count ?? 0, 'booking'), width: destinationWidth(r) })),
    },
    {
      title: 'Bookings by payment',
      meta: 'Count and booked value',
      tone: 'tone-data',
      empty: 'No bookings in this period.',
      rows: d.bookingsByPayment.map((r) => ({ label: stateLabel(r.label), value: count(r.value), detail: peso(r.revenue ?? 0), width: paymentWidth(r) })),
    },
    {
      title: 'Bookings by stage',
      meta: 'How far each booking has gone',
      tone: 'tone-data',
      empty: 'No bookings in this period.',
      rows: d.bookingsByStatus.map((r) => ({ label: stateLabel(r.label), value: count(r.value), detail: '', width: stageWidth(r) })),
    },
  ];
});

const breakdownColumns = computed(() => {
  const [byPackage, byCategory, byDestination, byPayment, byStage] = breakdowns.value;
  return byPackage ? [[byPackage, byDestination], [byCategory, byPayment, byStage]] : [];
});

// ----- Loading, print -----

async function load() {
  loading.value = data.value === null;
  error.value = '';
  try {
    if (!today.value) await loadToday();
    const r = range.value;
    data.value = await api.reports(r?.from ?? '', r?.to ?? '');
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Please check your connection and try again.';
  } finally {
    loading.value = false;
    await nextTick();
    attachChart();
  }
}

function printReport() {
  window.print();
}

watch(() => [range.value?.from, range.value?.to], () => void load());

onMounted(load);
</script>

<style scoped>
.report-period {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px 20px;
  padding-block: 14px;
}

.report-period-text {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.report-period-text strong {
  color: var(--color-text-primary);
  font-weight: 600;
}

.report-headline {
  white-space: normal;
}

/* These tables are sized for phones too, so the shared 720px minimum does not apply. */
.statement-table,
.month-table {
  min-width: 0;
}

.statement-table th .row-subtext {
  display: block;
  margin-top: 1px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  font-weight: 400;
}

/* Row labels are header cells; they share the rows' light rule and the body text size. */
.statement-table tbody th,
.month-table tbody th {
  border-bottom-color: var(--color-border-subtle);
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 500;
  text-align: left;
}

.statement-table tr.is-total th,
.statement-table tr.is-total td {
  border-top: 1px solid var(--color-border-strong);
}

.statement-table td strong {
  display: inline;
}

.dss-table .is-loss,
.dss-table .is-loss strong {
  color: var(--color-danger-ink);
}

.change {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  font-weight: 600;
  white-space: nowrap;
}

.change-icon {
  font-size: 10px;
}

.change.up {
  color: var(--color-success-ink);
}

.change.down {
  color: var(--color-danger-ink);
}

.change.flat {
  color: var(--color-text-secondary);
}

.statement-table .previous-inline {
  display: none;
}

.chart-key {
  display: inline-flex;
  gap: 16px;
}

.chart-key span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.chart-key i {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
}

.key-revenue,
.bar-revenue {
  background: var(--color-primary);
  fill: var(--color-primary);
}

.key-costs,
.bar-costs {
  background: #9AA6BC;
  fill: #9AA6BC;
}

.month-chart-box {
  margin: 4px 0 16px;
}

.month-chart {
  display: block;
  width: 100%;
  height: auto;
}

.grid-line {
  stroke: var(--color-border-subtle);
  stroke-width: 1;
}

.grid-line.base {
  stroke: var(--color-border-strong);
}

.axis-label {
  fill: var(--color-text-muted);
  font-family: var(--font-family);
  font-size: 11px;
  font-variant-numeric: lining-nums tabular-nums;
}

.month-table tbody th,
.month-table tfoot th {
  white-space: nowrap;
}

.month-table tfoot th {
  color: var(--color-text-primary);
  font-size: var(--text-base);
  text-align: left;
}

.month-table tfoot th,
.month-table tfoot td {
  border-top: 1px solid var(--color-border-strong);
  color: var(--color-text-primary);
  font-weight: 650;
}

.report-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  align-items: start;
  gap: 20px;
}

.report-column {
  display: grid;
  gap: 20px;
  min-width: 0;
}

@media (max-width: 1100px) {
  .report-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}

.report-bars {
  display: grid;
  gap: 12px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.report-bar-row {
  display: grid;
  grid-template-columns: minmax(0, 12rem) minmax(40px, 1fr) auto;
  align-items: center;
  gap: 12px;
}

.report-bar-label {
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.report-bar-track {
  position: relative;
  height: 9px;
  overflow: hidden;
  border-radius: var(--radius-pill);
  background: var(--color-surface-sunken);
}

.report-bar-track i {
  position: absolute;
  inset: 0 auto 0 0;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

.tone-data .report-bar-track i {
  background: #7C89A3;
}

.report-bar-value {
  display: grid;
  justify-items: end;
  text-align: right;
}

.report-bar-value strong {
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  font-weight: 650;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.report-bar-detail {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
}

.report-empty {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.report-error {
  display: grid;
  gap: 8px;
  justify-items: start;
}

.report-error-title {
  color: var(--color-text-primary);
  font-weight: 600;
}

.report-error-text {
  color: var(--color-danger-ink);
  font-size: var(--text-sm);
}

.report-foot {
  max-width: 90ch;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.print-heading {
  display: none;
}

.figure-short {
  display: none;
}

@media (max-width: 640px) {
  .previous-col,
  .count-col {
    display: none;
  }

  .statement-table .previous-inline {
    display: block;
  }

  .month-table .figure-full,
  .statement-table .figure-full {
    display: none;
  }

  .month-table .figure-short,
  .statement-table .figure-short {
    display: inline;
  }

  .statement-table th,
  .statement-table td,
  .month-table th,
  .month-table td {
    padding: 9px 5px;
    font-size: var(--text-sm);
  }

  .statement-table th:first-child,
  .month-table th:first-child {
    padding-left: 0;
  }

  .statement-table td:last-child,
  .month-table td:last-child,
  .statement-table th:last-child,
  .month-table th:last-child {
    padding-right: 0;
  }

  .report-bar-row {
    grid-template-columns: minmax(0, 6.5rem) minmax(32px, 1fr) auto;
    gap: 8px;
  }
}

/* Printing: the report on paper, without the app around it. */
@media print {
  .report-period :deep(.period-select) {
    display: none;
  }

  .print-heading {
    display: block;
    color: #000;
    font-size: 12px;
  }

  .month-chart rect {
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }
}
</style>
