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
        ProphetOps<template v-if="businessName"> · {{ businessName }}</template> · Report for {{ periodLabel }}
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
          </div>

          <!-- The period in a few plain sentences, before the table that holds the figures -->
          <ul v-if="findings.length" class="report-findings" aria-label="In short">
            <li v-for="(finding, index) in findings" :key="index"><template v-for="(part, partIndex) in finding" :key="partIndex"><strong v-if="part.strong" :class="part.tone">{{ part.text }}</strong><template v-else>{{ part.text }}</template></template></li>
          </ul>

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
                  <th v-if="data.previous" scope="col" class="num change-col">Change</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in statement" :key="row.label" :class="{ 'is-total': row.total }">
                  <th scope="row">
                    {{ row.label }}
                    <span v-if="row.note" class="row-subtext">{{ row.note }}</span>
                  </th>
                  <td class="num" :class="{ 'is-loss': row.loss }">
                    <strong>{{ row.current }}</strong>
                    <span v-if="data.previous" class="change-inline">
                      <span class="change" :class="row.change.tone">
                        <UiIcon v-if="row.change.icon" :name="row.change.icon" class="change-icon" />
                        {{ row.change.text }}
                      </span>
                      <span class="change-from">from {{ row.previousShort }}</span>
                    </span>
                  </td>
                  <td v-if="data.previous" class="num previous-col">{{ row.previous }}</td>
                  <td v-if="data.previous" class="num change-col">
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
              tabindex="0"
              @pointerdown="pointAt"
              @pointermove="pointAt"
              @pointerleave="leaveChart"
              @focus="focusChart"
              @blur="active = null"
              @keydown="stepMonth"
            >
              <rect
                v-if="activeBar"
                class="month-band"
                :x="activeBar.center - activeBar.band / 2"
                :y="plotTop"
                :width="activeBar.band"
                :height="plotBottom - plotTop"
              />
              <g v-for="line in gridLines" :key="line.value">
                <line class="grid-line" :class="{ base: line.value === 0 }" :x1="plotLeft" :x2="plotRight" :y1="line.y" :y2="line.y" />
                <text class="axis-label" :x="plotLeft - 8" :y="line.y" text-anchor="end" dominant-baseline="middle">{{ line.label }}</text>
              </g>
              <g v-for="bar in bars" :key="bar.month">
                <path class="bar-revenue" :d="column(bar.x, bar.revenueY, bar.width)" />
                <path class="bar-costs" :d="column(bar.x + bar.width + 2, bar.costsY, bar.width)" />
                <text v-if="bar.showLabel" class="axis-label" :x="bar.center" :y="plotBottom + 18" text-anchor="middle">{{ bar.short }}</text>
              </g>
            </svg>

            <!-- The month being read, beside its columns. The table below holds the same figures. -->
            <div v-if="activeMonth" ref="tip" class="month-tip" :style="tipPosition" aria-hidden="true">
              <p class="month-tip-title">{{ activeMonth.name }}</p>
              <p class="month-tip-row"><i class="tip-key key-revenue"></i><strong>{{ peso(activeMonth.revenue) }}</strong>Revenue</p>
              <p class="month-tip-row"><i class="tip-key key-costs"></i><strong>{{ peso(activeMonth.costs) }}</strong>Costs</p>
              <p class="month-tip-row month-tip-net" :class="{ 'is-loss': activeMonth.afterCosts < 0 }">
                <i class="tip-key"></i><strong>{{ peso(activeMonth.afterCosts) }}</strong>After costs
              </p>
            </div>
          </div>

          <div v-if="data.monthly.length" class="table-scroll">
            <table class="dss-table month-table">
              <thead>
                <tr>
                  <th scope="col">Month</th>
                  <th scope="col" class="num count-col">Bookings</th>
                  <th scope="col" class="num count-col">Passengers</th>
                  <th scope="col" class="num">Revenue</th>
                  <th scope="col" class="num cost-col">Costs</th>
                  <th scope="col" class="num">After costs</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="m in data.monthly" :key="m.month">
                  <th scope="row">{{ m.label }}</th>
                  <td class="num count-col">{{ count(m.bookings) }}</td>
                  <td class="num count-col">{{ count(m.passengers) }}</td>
                  <td class="num">
                    <span class="figure-full">{{ peso(m.revenue) }}</span><span class="figure-short">{{ pesoCompact(m.revenue) }}</span>
                    <span class="fold-line">costs {{ pesoCompact(m.costs) }}</span>
                  </td>
                  <td class="num cost-col"><span class="figure-full">{{ peso(m.costs) }}</span><span class="figure-short">{{ pesoCompact(m.costs) }}</span></td>
                  <td class="num" :class="{ 'is-loss': m.afterCosts < 0 }"><span class="figure-full">{{ peso(m.afterCosts) }}</span><span class="figure-short">{{ pesoCompact(m.afterCosts) }}</span></td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <th scope="row">Total</th>
                  <td class="num count-col">{{ count(data.counts.bookings) }}</td>
                  <td class="num count-col">{{ count(data.counts.passengers) }}</td>
                  <td class="num">
                    <span class="figure-full">{{ peso(data.revenue) }}</span><span class="figure-short">{{ pesoCompact(data.revenue) }}</span>
                    <span class="fold-line">costs {{ pesoCompact(data.costs) }}</span>
                  </td>
                  <td class="num cost-col"><span class="figure-full">{{ peso(data.costs) }}</span><span class="figure-short">{{ pesoCompact(data.costs) }}</span></td>
                  <td class="num" :class="{ 'is-loss': data.profit < 0 }"><span class="figure-full">{{ peso(data.profit) }}</span><span class="figure-short">{{ pesoCompact(data.profit) }}</span></td>
                </tr>
              </tfoot>
            </table>
          </div>

          <p v-else class="report-empty">Nothing is recorded in this period.</p>
        </section>

        <!-- 3. Where the money came from and where it went, side by side, then the bookings behind it -->
        <div class="report-grid">
          <section
            v-for="panel in breakdownPanels"
            :key="panel.id"
            class="content-panel"
            :class="{ 'report-wide': panel.wide }"
            :aria-labelledby="`${panel.id}-title`"
          >
            <div class="panel-header report-panel-header">
              <div class="panel-title-group">
                <h2 :id="`${panel.id}-title`">{{ panel.title }}</h2>
              </div>
              <div v-if="panel.id === 'sources'" class="report-switch" role="group" aria-label="Show revenue by">
                <button type="button" class="filter-chip" :aria-pressed="source === 'package'" @click="source = 'package'">Packages</button>
                <button type="button" class="filter-chip" :aria-pressed="source === 'destination'" @click="source = 'destination'">Destinations</button>
              </div>
            </div>
            <div class="report-groups">
              <div v-for="group in panel.groups" :key="group.key" class="report-group">
                <h3 v-if="group.heading" class="report-group-title">{{ group.heading }}</h3>
                <ul v-if="group.rows.length" :id="`breakdown-${group.key}`" class="report-bars" :class="group.tone">
                  <li
                    v-for="(row, rowIndex) in group.rows"
                    :key="row.label"
                    class="report-bar-row"
                    :class="{ 'is-hidden': rowIndex >= TOP_ROWS && !expanded.has(group.key) }"
                  >
                    <span class="report-bar-label">{{ row.label }}</span>
                    <span class="report-bar-track"><i :style="{ width: row.width }"></i></span>
                    <span class="report-bar-value">
                      <strong>{{ row.value }}</strong>
                      <span v-if="row.detail" class="report-bar-detail">{{ row.detail }}</span>
                    </span>
                  </li>
                </ul>
                <p v-else class="report-empty">{{ group.empty }}</p>
                <button
                  v-if="group.rows.length > TOP_ROWS"
                  type="button"
                  class="report-more"
                  :aria-expanded="expanded.has(group.key)"
                  :aria-controls="`breakdown-${group.key}`"
                  @click="toggleRows(group.key)"
                >
                  {{ expanded.has(group.key) ? 'Show the top five' : `Show all ${group.rows.length}` }}
                </button>
              </div>
            </div>
          </section>
        </div>

        <p class="report-foot">
          Generated {{ data.generatedAt }}. Revenue is gross booking revenue and leaves out voided bookings; costs leave
          out voided expenses. Revenue after costs is revenue less recorded expenses, not accounting profit.
          <button type="button" class="report-help" @click="openHelp('reports')">How these figures are worked out</button>
        </p>
      </template>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import AppShell from '../components/AppShell.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import Skeleton from '../components/Skeleton.vue';
import UiIcon, { type UiIconName } from '../components/UiIcon.vue';
import { useBusinessDate } from '../composables/useBusinessDate';
import { useContentWidth } from '../composables/useContentWidth';
import { useQueryState } from '../composables/useQueryState';
import { openHelp } from '../composables/useHelp';
import { api, describeError, type ReportLine, type ReportsData } from '../api';
import { count, peso, pesoCompact, stateLabel } from '../format';
import { describeRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';

const data = ref<ReportsData | null>(null);
const loading = ref(true);
const error = ref('');
const { today, businessName, load: loadToday } = useBusinessDate();

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

// ----- In short: the period in a few plain sentences -----

interface Part {
  text: string;
  strong?: boolean;
  tone?: string;
}

const longMonthFormat = new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' });

/** "2026-03" reads "March 2026". */
function longMonth(month: string): string {
  const [year, number] = month.split('-').map(Number);
  return longMonthFormat.format(new Date(Date.UTC(year, number - 1, 1)));
}

/** The same day some months earlier, held inside a shorter month the way the server holds it. */
function monthsBefore(date: string, months: number): string {
  const [year, month, day] = date.split('-').map(Number);
  const first = new Date(Date.UTC(year, month - 1 - months, 1));
  const last = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0)).getUTCDate();
  return new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), Math.min(day, last))).toISOString().slice(0, 10);
}

/** What follows "than": "the same days last year", "in 2025", or the earlier period's dates. */
function earlierPeriod(d: ReportsData): string {
  const p = d.previous;
  if (!p?.from || !p.to) return 'the earlier period';
  const { from, to, through } = d.period;
  if (from && through && through !== to) {
    if (p.from === monthsBefore(from, 12) && p.to === monthsBefore(through, 12)) return 'the same days last year';
    if (p.from === monthsBefore(from, 1) && p.to === monthsBefore(through, 1)) return 'the same days last month';
  }
  const label = describeRange({ from: p.from, to: p.to });
  return /^(\d{4}|[A-Z][a-z]+ \d{4})$/.test(label) ? `in ${label}` : label;
}

/** "45.7% more than the same days last year", the change coloured as the summary table colours it. */
function against(current: number, previous: number, than: string): Part[] {
  const percent = ((current - previous) / previous) * 100;
  if (Math.abs(percent) < 0.05) return [{ text: `level with ${than.replace(/^in /, '')}.` }];
  return [
    { text: `${Math.abs(percent).toFixed(1)}% ${percent > 0 ? 'more' : 'less'}`, strong: true, tone: percent > 0 ? 'up' : 'down' },
    { text: ` than ${than}.` },
  ];
}

/** "March", "March and June", "March, June and July". */
function listOf(items: string[]): string {
  return items.length < 2 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

function plural(n: number, word: string): string {
  return `${count(n)} ${word}${n === 1 ? '' : 's'}`;
}

/** Up to four sentences: revenue, what was left after costs, the best month, and the leading package. */
const findings = computed<Part[][]>(() => {
  const d = data.value;
  if (!d) return [];
  if (!d.revenue && !d.costs && !d.counts.bookings) return [[{ text: 'No bookings or expenses were recorded in this period.' }]];

  const p = d.previous;
  const than = earlierPeriod(d);
  const lines: Part[][] = [];

  if (d.revenue > 0) {
    const line: Part[] = [{ text: 'Revenue came to ' }, { text: peso(d.revenue), strong: true }];
    if (!p) line.push({ text: ` from ${plural(d.counts.bookings, 'booking')}.` });
    else if (p.revenue > 0) line.push({ text: ', ' }, ...against(d.revenue, p.revenue, than));
    else line.push({ text: '; the earlier period had none.' });
    lines.push(line);
  } else {
    lines.push([{ text: 'No booking revenue was recorded in this period.' }]);
  }

  const kept: Part[] = d.profit < 0
    ? [{ text: 'Costs were ' }, { text: peso(-d.profit), strong: true, tone: 'down' }, { text: ' more than revenue.' }]
    : [{ text: 'After costs, ' }, { text: peso(d.profit), strong: true }, { text: ' was left' }];
  if (d.profit >= 0) {
    if (p && p.profit > 0 && d.profit > 0) kept.push({ text: ', ' }, ...against(d.profit, p.profit, than));
    else kept.push({ text: '.' });
  }
  const losses = d.monthly.filter((m) => m.afterCosts < 0);
  if (d.monthly.length > 1 && losses.length) {
    if (losses.length === d.monthly.length) kept.push({ text: ' Every month ended with costs above revenue.' });
    else if (losses.length <= 3) kept.push({ text: ' ' }, { text: listOf(losses.map((m) => longMonth(m.month))), strong: true }, { text: ` ended with costs above revenue.` });
    else kept.push({ text: ` ${losses.length} months ended with costs above revenue; the table below shows which.` });
  }
  if (d.revenue > 0 || d.costs > 0) lines.push(kept);

  if (d.monthly.length > 1 && d.revenue > 0) {
    const best = d.monthly.reduce((a, b) => (b.revenue > a.revenue ? b : a));
    lines.push([{ text: 'Revenue was highest in ' }, { text: longMonth(best.month), strong: true }, { text: `, at ${peso(best.revenue)}.` }]);
  }

  const top = d.revenueByPackage[0];
  if (top && d.revenue > 0) {
    lines.push(d.revenueByPackage.length === 1
      ? [{ text: 'All revenue came from ' }, { text: top.label, strong: true }, { text: '.' }]
      : [{ text: top.label, strong: true }, { text: ` brought in the most revenue: ${peso(top.value)}, ${share(top.value, d.revenue)} of the total.` }]);
  }
  return lines;
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
      short: month === '01' || i === 0 ? `${short} ${year.slice(2)}` : short,
      center,
      band,
      x: center - width - 1,
      width,
      revenueY: plotBottom - height(m.revenue),
      costsY: plotBottom - height(m.costs),
      showLabel: i % stride === 0,
    };
  });
});

/** A column with a 4px rounded top, square where it stands on the baseline. */
function column(x: number, y: number, width: number): string {
  const height = plotBottom - y;
  if (height <= 0) return '';
  const r = Math.min(4, width / 2, height);
  return `M${x},${plotBottom}V${y + r}A${r},${r} 0 0 1 ${x + r},${y}H${x + width - r}A${r},${r} 0 0 1 ${x + width},${y + r}V${plotBottom}Z`;
}

const chartLabel = computed(() => {
  const months = data.value?.monthly ?? [];
  if (!months.length) return 'Monthly revenue and costs. Nothing recorded.';
  return 'Monthly revenue and costs, oldest first: '
    + months.map((m) => `${m.label} revenue ${peso(m.revenue)}, costs ${peso(m.costs)}`).join('; ');
});

// The month being read: under the pointer, under a finger (a tap, or a slide across the chart),
// or stepped to with the arrow keys. Null while no month is being read.
const active = ref<number | null>(null);
const tip = ref<HTMLElement | null>(null);
const tipWidth = ref(180);

const activeBar = computed(() => (active.value === null ? null : bars.value[active.value] ?? null));

const activeMonth = computed(() => {
  const m = active.value === null ? null : data.value?.monthly[active.value];
  return m ? { ...m, name: longMonth(m.month) } : null;
});

/** Beside the month's columns, on whichever side has room, so the columns themselves stay in view. */
const tipPosition = computed(() => {
  const bar = activeBar.value;
  if (!bar) return {};
  const width = tipWidth.value;
  const right = bar.center + bar.band / 2 + 8;
  const left = bar.center - bar.band / 2 - 8 - width;
  // On a narrow phone neither side may have room: the readout then sits against the far edge,
  // away from the month, so as little of its columns as possible is covered.
  const x = right + width <= chartWidth.value ? right
    : left >= 0 ? left
    : bar.center > chartWidth.value / 2 ? 0 : Math.max(0, chartWidth.value - width);
  return { left: `${x}px`, top: `${plotTop}px` };
});

watch(active, async () => {
  await nextTick();
  if (tip.value) tipWidth.value = tip.value.offsetWidth;
});

watch(data, () => {
  active.value = null;
});

function pointAt(event: PointerEvent) {
  const box = (event.currentTarget as SVGSVGElement).getBoundingClientRect();
  const months = bars.value.length;
  if (!months || !box.width) return;
  const x = ((event.clientX - box.left) / box.width) * chartWidth.value;
  const band = (plotRight.value - plotLeft) / months;
  active.value = Math.max(0, Math.min(months - 1, Math.floor((x - plotLeft) / band)));
}

function leaveChart(event: PointerEvent) {
  // A lifted finger also "leaves"; a tapped month stays open until a tap somewhere else.
  if (event.pointerType === 'mouse') active.value = null;
}

function focusChart() {
  if (active.value === null) active.value = 0;
}

function stepMonth(event: KeyboardEvent) {
  const last = bars.value.length - 1;
  const current = active.value ?? 0;
  const moves: Record<string, number> = { ArrowRight: current + 1, ArrowLeft: current - 1, Home: 0, End: last };
  if (event.key === 'Escape') active.value = null;
  else if (event.key in moves) active.value = Math.max(0, Math.min(last, moves[event.key]));
  else return;
  event.preventDefault();
}

function closeOnTapElsewhere(event: PointerEvent) {
  if (active.value !== null && !chartBox.value?.contains(event.target as Node)) active.value = null;
}

onMounted(() => document.addEventListener('pointerdown', closeOnTapElsewhere));
onBeforeUnmount(() => document.removeEventListener('pointerdown', closeOnTapElsewhere));

// ----- Breakdowns -----

/** A bar fills its share of the whole, so a line marked 26% fills a quarter of its track. */
function shareWidth(value: number, total: number): string {
  if (value <= 0 || total <= 0) return '0%';
  return `${Math.max(1.5, Math.min(100, (value / total) * 100))}%`;
}

/** A line's share of its total, as a whole percentage. */
function share(value: number, total: number): string {
  if (total <= 0) return '';
  const percent = (value / total) * 100;
  return percent > 0 && percent < 1 ? 'under 1%' : `${Math.round(percent)}%`;
}

function barRows(
  lines: ReportLine[],
  total: number,
  value: (line: ReportLine) => string,
  detail: (line: ReportLine) => string,
  label: (line: ReportLine) => string = (line) => line.label,
) {
  return lines.map((line) => ({ label: label(line), value: value(line), detail: detail(line), width: shareWidth(line.value, total) }));
}

// Each list shows its top five; the rest are a click away on screen, and always printed.
const TOP_ROWS = 5;
const expanded = reactive(new Set<string>());

function toggleRows(key: string) {
  if (expanded.has(key)) expanded.delete(key);
  else expanded.add(key);
}

// Revenue reads by package first, as the agency sells it, or by destination.
const source = ref<'package' | 'destination'>('package');

const breakdownPanels = computed(() => {
  const d = data.value;
  if (!d) return [];
  const byPackage = source.value === 'package';
  const noBookings = 'No bookings in this period.';
  const shareOf = (line: ReportLine, total: number, word: string) =>
    [share(line.value, total), plural(line.count ?? 0, word)].filter(Boolean).join(' · ');
  return [
    {
      id: 'sources',
      title: 'Where revenue came from',
      wide: false,
      groups: [{
        key: byPackage ? 'packages' : 'destinations',
        heading: '',
        tone: 'tone-primary',
        empty: noBookings,
        rows: barRows(byPackage ? d.revenueByPackage : d.revenueByDestination, d.revenue, (line) => peso(line.value),
          (line) => shareOf(line, d.revenue, 'booking')),
      }],
    },
    {
      id: 'spending',
      title: 'Spending by category',
      wide: false,
      groups: [{
        key: 'categories',
        heading: '',
        tone: 'tone-data',
        empty: 'No expenses in this period.',
        rows: barRows(d.expensesByCategory, d.costs, (line) => peso(line.value),
          (line) => shareOf(line, d.costs, 'expense')),
      }],
    },
    {
      id: 'bookings',
      title: 'Bookings',
      wide: true,
      groups: d.counts.bookings
        ? [
            { key: 'payment', heading: 'Payment status', tone: 'tone-data', empty: noBookings,
              rows: barRows(d.bookingsByPayment, d.counts.bookings, (line) => count(line.value),
                (line) => `${share(line.value, d.counts.bookings)} · worth ${peso(line.revenue ?? 0)}`, (line) => stateLabel(line.label)) },
            { key: 'stage', heading: 'Booking status', tone: 'tone-data', empty: noBookings,
              rows: barRows(d.bookingsByStatus, d.counts.bookings, (line) => count(line.value),
                (line) => share(line.value, d.counts.bookings), (line) => stateLabel(line.label)) },
          ]
        : [{ key: 'payment', heading: '', tone: 'tone-data', empty: noBookings, rows: [] }],
    },
  ];
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
    error.value = describeError(err, 'Check the connection, then try again.');
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

.report-findings {
  display: grid;
  gap: 6px;
  max-width: 72ch;
  margin: 0 0 20px;
  padding: 0;
  list-style: none;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.5;
  text-wrap: pretty;
}

.report-findings li {
  position: relative;
  padding-left: 18px;
}

.report-findings li::before {
  content: '';
  position: absolute;
  top: 0.65em;
  left: 2px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-primary);
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

.report-findings strong {
  color: var(--color-text-primary);
  font-weight: 650;
  font-variant-numeric: lining-nums tabular-nums;
}

.report-findings strong.up {
  color: var(--color-success-ink);
}

.report-findings strong.down {
  color: var(--color-danger-ink);
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
  background: var(--color-costs);
  fill: var(--color-costs);
}

.month-chart-box {
  position: relative;
  margin: 4px 0 16px;
}

/* A finger sliding sideways reads month after month; sliding up and down still scrolls the page. */
.month-chart {
  display: block;
  width: 100%;
  height: auto;
  border-radius: var(--radius-sm);
  touch-action: pan-y;
  -webkit-tap-highlight-color: transparent;
}

.month-chart:focus {
  outline: none;
}

.month-chart:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 4px;
}

.month-band {
  fill: var(--color-primary-soft);
}

/* One set of columns for every line, so the figures line up under each other. */
.month-tip {
  position: absolute;
  z-index: 2;
  display: grid;
  grid-template-columns: 12px auto auto;
  gap: 4px 8px;
  padding: 10px 12px;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
  font-size: var(--text-sm);
  white-space: nowrap;
  pointer-events: none;
}

.month-tip-title {
  grid-column: 1 / -1;
  margin-bottom: 2px;
  color: var(--color-text-primary);
  font-weight: 600;
}

.month-tip-row {
  display: grid;
  grid-column: 1 / -1;
  grid-template-columns: subgrid;
  align-items: center;
  color: var(--color-text-muted);
}

.month-tip-row strong {
  color: var(--color-text-primary);
  font-weight: 650;
  text-align: right;
  font-variant-numeric: lining-nums tabular-nums;
}

/* Series keys are short strokes in the series colour; the text keeps its own ink. */
.tip-key {
  height: 3px;
  border-radius: 2px;
}

.month-tip-net {
  margin-top: 2px;
  padding-top: 6px;
  border-top: 1px solid var(--color-border-subtle);
}

.month-tip-net.is-loss strong {
  color: var(--color-danger-ink);
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

/* Revenue and spending sit side by side; the bookings panel spans both, its two lists in columns. */
.report-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
}

.report-grid > .content-panel {
  min-width: 0;
}

.report-wide {
  grid-column: 1 / -1;
}

.report-groups {
  display: grid;
  gap: 24px;
}

.report-wide .report-groups {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 40px;
}

.report-group-title {
  margin-bottom: 12px;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 600;
}

@media (max-width: 1100px) {
  .report-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}

.report-panel-header {
  flex-wrap: wrap;
  align-items: center;
  row-gap: 10px;
}

.report-switch {
  display: flex;
  gap: 6px;
}

/* The name sits above its bar, so a long package name reads in full at any width; the figure
   lines up with the name and its detail with the bar. The rows share one pair of columns, so
   every bar in a list has the same full length and the bars compare fairly. */
.report-bars {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 14px 16px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.report-bar-row {
  display: grid;
  grid-column: 1 / -1;
  grid-template-columns: subgrid;
  align-items: center;
  row-gap: 6px;
}

.report-bar-row.is-hidden {
  display: none;
}

.report-more {
  justify-self: start;
  min-height: 32px;
  margin-top: 10px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.report-more:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.report-bar-label {
  grid-column: 1;
  grid-row: 1;
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  overflow-wrap: anywhere;
}

.report-bar-track {
  grid-column: 1;
  grid-row: 2;
  position: relative;
  height: 8px;
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
  background: var(--color-costs-ink);
}

.report-bar-value {
  display: contents;
}

.report-bar-value strong {
  grid-column: 2;
  grid-row: 1;
  justify-self: end;
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  font-weight: 650;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.report-bar-detail {
  grid-column: 2;
  grid-row: 2;
  justify-self: end;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
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
  /* Inter's average letter is narrower than the 0 that "ch" measures, so 60ch holds about 75. */
  max-width: 60ch;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.report-help {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-weight: 600;
  text-decoration: underline;
  text-underline-offset: 3px;
  cursor: pointer;
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

  .report-wide .report-groups {
    grid-template-columns: minmax(0, 1fr);
  }

  .report-switch {
    width: 100%;
  }

  .report-switch .filter-chip {
    flex: 1;
    justify-content: center;
    min-height: 40px;
  }

  .month-tip {
    gap: 3px 6px;
    padding: 8px 10px;
    font-size: var(--text-xs);
  }
}

.change-inline,
.fold-line {
  display: none;
}

/* Narrow phones: the change moves under the period's figure instead of needing a column of its own. */
@media (max-width: 420px) {
  .statement-table .change-col {
    display: none;
  }

  .statement-table th:nth-child(2),
  .statement-table td:nth-child(2) {
    padding-right: 0;
  }

  .statement-table .change-inline {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 0 6px;
    margin-top: 2px;
    font-size: var(--text-xs);
  }

  .change-from {
    color: var(--color-text-muted);
    font-weight: 400;
    white-space: nowrap;
  }
}

/* The narrowest phones: costs go under revenue, so the month, revenue and what is left fit. */
@media (max-width: 380px) {
  .month-table .cost-col {
    display: none;
  }

  .month-table .fold-line {
    display: block;
    margin-top: 1px;
    color: var(--color-text-muted);
    font-size: var(--text-xs);
    font-weight: 400;
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

  .month-chart path {
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }

  .month-band,
  .month-tip {
    display: none;
  }

  /* An A4 page is narrower than the screen breakpoint; the breakdowns keep their two columns
     there, so the report prints on fewer pages, and no panel splits across a page break. */
  .report-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    align-items: start;
    gap: 12px;
  }

  .report-grid > .content-panel {
    break-inside: avoid;
  }

  /* On paper the switch only names the list that is shown. */
  .report-switch .filter-chip[aria-pressed='false'] {
    display: none;
  }

  .report-bar-row.is-hidden {
    display: grid;
  }

  .report-more {
    display: none;
  }
}
</style>
