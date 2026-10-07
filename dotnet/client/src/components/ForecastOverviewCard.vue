<template>
  <section class="overview-tool" aria-label="Forecast overview">
    <!-- Top Section: White Chart -->
    <div ref="chartBox" class="overview-chart-section forecast-chart">
      <!-- Header Row: Title & Segmented Controls -->
      <div class="overview-header-row">
        <div class="overview-title-group">
          <h2 class="overview-title">{{ title }}</h2>
        </div>
        <div class="overview-header-actions">
          <RouterLink
            v-if="showForecastLink"
            to="/forecast"
            class="compact-action-link view-full-link"
          >
            View full forecast <span aria-hidden="true">→</span>
          </RouterLink>
          <div class="history-toggle-group" role="group" aria-label="Recorded history display range">
            <button
              type="button"
              class="history-toggle-btn"
              :class="{ active: historyMonths === 6 }"
              :aria-pressed="historyMonths === 6"
              @click="setHistoryMonths(6)"
            >
              Past 6 months
            </button>
            <button
              type="button"
              class="history-toggle-btn"
              :class="{ active: historyMonths === 12 }"
              :aria-pressed="historyMonths === 12"
              @click="setHistoryMonths(12)"
            >
              Past 12 months
            </button>
          </div>
        </div>
      </div>

      <!-- Legend Row -->
      <div class="overview-legend-row" aria-label="Chart legend">
        <div class="legend-item">
          <svg class="legend-swatch-line" width="24" height="14" aria-hidden="true">
            <line x1="0" y1="7" x2="24" y2="7" stroke="#233e69" stroke-width="2.5" />
            <circle cx="12" cy="7" r="2.5" fill="#233e69" />
          </svg>
          <span>{{ isNarrow ? 'Recorded' : 'Recorded revenue' }}</span>
        </div>
        <div class="legend-item">
          <svg class="legend-swatch-line" width="24" height="14" aria-hidden="true">
            <line x1="0" y1="7" x2="24" y2="7" stroke="#13796c" stroke-width="2.5" stroke-dasharray="5 4" />
            <circle cx="12" cy="7" r="2.5" fill="#ffffff" stroke="#13796c" stroke-width="1.5" />
          </svg>
          <span>{{ isNarrow ? 'Forecast' : 'Forecast revenue' }}</span>
        </div>
        <div class="legend-item">
          <span class="legend-swatch-band" aria-hidden="true"></span>
          <span>80% prediction range</span>
        </div>
      </div>

      <!-- Responsive SVG Plot -->
      <div v-if="chartModel" class="overview-plot-wrapper">
        <svg
          :viewBox="`0 0 ${chartModel.width} ${chartModel.height}`"
          class="overview-svg"
          role="group"
          aria-label="Recorded booking revenue and 6-month forecast chart"
        >
          <!-- Labels above plot -->
          <text :x="chartModel.plotLeft" :y="chartModel.plotTop - 12" font-size="13" fill="#57616d">
            Revenue (PHP)
          </text>
          <text :x="chartModel.plotRight" :y="chartModel.plotTop - 12" font-size="13" fill="#18675e" text-anchor="end">
            Next 6 months
          </text>

          <!-- Forecast region subtle tint background -->
          <rect
            :x="chartModel.dividerX"
            :y="chartModel.plotTop"
            :width="chartModel.plotRight - chartModel.dividerX"
            :height="chartModel.plotHeight"
            fill="#f5f9f7"
          />

          <!-- Shaded highlight region (when a recommendation is selected) -->
          <rect
            v-if="chartModel.highlightRegion"
            :x="chartModel.highlightRegion.x"
            :y="chartModel.plotTop"
            :width="chartModel.highlightRegion.width"
            :height="chartModel.plotHeight"
            :fill="chartModel.highlightRegion.color"
            fill-opacity="0.1"
            pointer-events="none"
          />

          <!-- Horizontal grid lines -->
          <g class="overview-grid-h" aria-hidden="true">
            <line
              v-for="t in chartModel.yTicks"
              :key="`y-grid-${t.value}`"
              :x1="chartModel.plotLeft"
              :x2="chartModel.plotRight"
              :y1="t.y"
              :y2="t.y"
              stroke="#cbd5df"
              stroke-width="1"
            />
          </g>

          <!-- Y-axis numeric labels -->
          <g class="overview-axis-y" aria-hidden="true">
            <text
              v-for="t in chartModel.yTicks"
              :key="`y-lbl-${t.value}`"
              :x="chartModel.plotLeft - 10"
              :y="t.y + 4"
              font-size="12"
              fill="#485664"
              text-anchor="end"
            >
              {{ t.label }}
            </text>
          </g>

          <!-- Vertical grid lines -->
          <g class="overview-grid-v" aria-hidden="true">
            <line
              v-for="e in chartModel.entries"
              :key="`v-grid-${e.index}`"
              :x1="e.x"
              :x2="e.x"
              :y1="chartModel.plotTop"
              :y2="chartModel.plotBottom"
              stroke="#dce3e9"
              stroke-width="1"
            />
          </g>

          <!-- Prediction Interval Band -->
          <path :d="chartModel.bandPath" fill="#c6e0d6" fill-opacity="0.55" />

          <!-- Recorded Series Line (Navy) -->
          <path :d="chartModel.historyPath" fill="none" stroke="#233e69" stroke-width="2.5" stroke-linejoin="round" />

          <!-- Forecast Series Line (Teal Dashed) -->
          <path :d="chartModel.forecastPath" fill="none" stroke="#13796c" stroke-width="2.5" stroke-dasharray="6 4" stroke-linejoin="round" />

          <!-- Entry circle markers -->
          <g class="overview-markers" aria-hidden="true">
            <circle
              v-for="e in chartModel.entries"
              :key="`pt-${e.index}`"
              :cx="e.x"
              :cy="e.y"
              :r="activeMonthIndex === e.index ? 5.5 : 3.5"
              :fill="e.isForecast ? '#ffffff' : '#233e69'"
              :stroke="e.isForecast ? '#13796c' : '#ffffff'"
              :stroke-width="e.isForecast ? 2 : 1"
            />
          </g>

          <!-- Highlight circle on selected recommendation month(s) -->
          <template v-if="chartModel.highlightRegion">
            <circle
              v-for="(pt, pIdx) in chartModel.highlightRegion.points"
              :key="`hl-pt-${pIdx}`"
              :cx="pt.x"
              :cy="pt.y"
              r="6"
              fill="#ffffff"
              :stroke="chartModel.highlightRegion.color"
              stroke-width="2.5"
              pointer-events="none"
            />
          </template>

          <!-- Active Month Hover/Focus Guide Line -->
          <line
            v-if="activeEntry"
            :x1="activeEntry.x"
            :y1="chartModel.plotTop"
            :x2="activeEntry.x"
            :y2="chartModel.plotBottom"
            stroke="#13796c"
            stroke-width="1.5"
            stroke-dasharray="4 3"
            pointer-events="none"
          />

          <!-- Month Label Ticks along bottom -->
          <g class="overview-axis-x" aria-hidden="true">
            <text
              v-for="lbl in chartModel.xTicks"
              :key="`x-lbl-${lbl.index}`"
              :x="lbl.x"
              :y="chartModel.plotBottom + 18"
              text-anchor="middle"
              font-size="12"
              fill="#26323d"
              font-weight="600"
            >
              {{ lbl.shortMonth }}
            </text>
            <text
              v-for="lbl in chartModel.xTicks"
              :key="`x-yr-${lbl.index}`"
              :x="lbl.x"
              :y="chartModel.plotBottom + 32"
              text-anchor="middle"
              font-size="11"
              fill="#57616d"
            >
              {{ lbl.year ?? '' }}
            </text>
          </g>

          <!-- Divider between history and forecast -->
          <line
            :x1="chartModel.dividerX"
            :y1="chartModel.plotTop"
            :x2="chartModel.dividerX"
            :y2="chartModel.plotBottom"
            stroke="#5c7a6e"
            stroke-width="1.5"
            stroke-dasharray="4 4"
          />

          <!-- Perimeter Plot Border -->
          <rect
            :x="chartModel.plotLeft"
            :y="chartModel.plotTop"
            :width="chartModel.plotRight - chartModel.plotLeft"
            :height="chartModel.plotHeight"
            fill="none"
            stroke="#8094a6"
            stroke-width="1.5"
            pointer-events="none"
          />

          <!-- Interactive Hit-Zones for hover/tap/keyboard navigation -->
          <g
            class="overview-hit-targets"
            role="group"
            aria-label="Monthly details"
            @pointerleave="clearActiveMonth"
            @focusout="onTargetsFocusOut"
          >
            <g
              v-for="(e, idx) in chartModel.entries"
              :key="`hit-g-${e.index}`"
              role="button"
              :tabindex="activeMonthIndex === idx || (activeMonthIndex === -1 && idx === 0) ? 0 : -1"
              :aria-label="monthAriaLabel(e)"
              class="month-target"
              @pointerenter="setActiveMonth(idx)"
              @focus="setActiveMonth(idx)"
              @click="setActiveMonth(idx)"
              @keydown="handleMonthKeydown($event, idx)"
            >
              <rect
                :x="hitBounds(idx).left"
                :y="chartModel.plotTop"
                :width="hitBounds(idx).width"
                :height="chartModel.plotHeight"
                fill="transparent"
                class="month-hit-rect"
              />
            </g>
          </g>

          <!-- Interactive Tooltip Callout Card -->
          <g
            v-if="activeEntry"
            class="chart-tooltip-group"
            role="status"
            aria-live="polite"
            aria-atomic="true"
            pointer-events="none"
            :transform="`translate(${tooltipX} ${tooltipY})`"
          >
            <rect
              :width="tooltipWidth"
              :height="tooltipHeight"
              rx="5"
              fill="#ffffff"
              stroke="#aab9c8"
              stroke-width="1.5"
            />
            <text x="14" y="27" font-size="15" font-weight="600" fill="#26323d">
              {{ activeEntry.fullDate }}
            </text>

            <!-- Forecast Month Tooltip Content -->
            <template v-if="activeEntry.isForecast">
              <text x="14" y="55" font-size="13" fill="#57616d">Forecast revenue</text>
              <text :x="tooltipWidth - 14" y="55" font-size="14" font-weight="600" fill="#26323d" text-anchor="end">
                {{ peso(activeEntry.value) }}
              </text>
              <text x="14" y="81" font-size="13" fill="#57616d">Range (approx. 80%)</text>
              <text x="14" y="102" font-size="13" fill="#26323d">
                {{ peso(activeEntry.lower ?? activeEntry.value) }} – {{ peso(activeEntry.upper ?? activeEntry.value) }}
              </text>
            </template>

            <!-- Recorded Month Tooltip Content -->
            <template v-else>
              <text x="14" y="55" font-size="13" fill="#57616d">Recorded revenue</text>
              <text :x="tooltipWidth - 14" y="55" font-size="14" font-weight="600" fill="#26323d" text-anchor="end">
                {{ peso(activeEntry.value) }}
              </text>

              <!-- If matched recorded expenses available -->
              <template v-if="activeEntryExpenses">
                <text x="14" y="81" font-size="13" fill="#57616d">Recorded expenses</text>
                <text :x="tooltipWidth - 14" y="81" font-size="14" font-weight="600" fill="#26323d" text-anchor="end">
                  {{ peso(activeEntryExpenses.total) }}
                </text>
                <line x1="14" :x2="tooltipWidth - 14" y1="96" y2="96" stroke="#dce3e9" stroke-width="1" />
                <template v-for="(cat, cIdx) in activeEntryExpenses.categories" :key="`cat-${cIdx}`">
                  <text x="14" :y="117 + cIdx * 23" font-size="12.5" fill="#57616d">{{ cat.name }}</text>
                  <text :x="tooltipWidth - 14" :y="117 + cIdx * 23" font-size="12.5" fill="#26323d" text-anchor="end">
                    {{ peso(cat.amount) }}
                  </text>
                </template>
              </template>
            </template>
          </g>
        </svg>
      </div>

      <!-- Concise Source & Honest Model Provenance Footers -->
      <div class="overview-footer-meta">
        <span class="overview-meta-item">{{ sourceNote }}</span>
        <span class="overview-meta-item">{{ modelInfoNote }}</span>
      </div>
    </div>

    <!-- Bottom Section: Lightly Tinted Green Recommendations Area (#edf3ef) -->
    <div
      v-if="showRecommendations && recommendations.length"
      class="overview-recommendations-section"
      aria-label="Action recommendations"
    >
      <div
        v-for="rec in recommendations"
        :key="rec.id"
        class="rec-row"
        :class="{
          selected: selectedRecId === rec.id,
          [`rec-kind-${rec.kind}`]: true
        }"
        role="button"
        tabindex="0"
        :aria-pressed="selectedRecId === rec.id"
        :aria-label="`${rec.action}. Highlight related forecast months.`"
        @click="toggleRec(rec.id)"
        @keydown.enter.space.prevent="toggleRec(rec.id)"
      >
        <!-- Left: Action Heading and Finding with Indicator Triangle -->
        <div class="rec-main-col">
          <h3 class="rec-action-heading">{{ rec.action }}</h3>
          <div class="rec-finding-line">
            <svg class="rec-triangle-svg" width="16" height="14" viewBox="0 0 16 14" aria-hidden="true">
              <!-- Increase: Green Up Triangle -->
              <polygon v-if="rec.kind === 'increase'" points="0,14 8,0 16,14" fill="#126c5f" />
              <!-- Decrease: Red Down Triangle -->
              <polygon v-else-if="rec.kind === 'decrease'" points="0,0 16,0 8,14" fill="#a33d41" />
              <!-- Uncertainty: Amber Warning Triangle with Exclamation Mark -->
              <g v-else>
                <polygon points="0,14 8,0 16,14" fill="#8c5b0a" />
                <line x1="8" y1="4" x2="8" y2="8" stroke="#ffffff" stroke-width="1.8" stroke-linecap="round" />
                <circle cx="8" cy="11" r="0.9" fill="#ffffff" />
              </g>
            </svg>
            <span class="rec-finding-text" :class="`finding-kind-${rec.kind}`">{{ rec.finding }}</span>
          </div>
        </div>

        <!-- Right / Stacking: Evidence Mini-Visual -->
        <div class="rec-evidence-col">
          <!-- Comparison Mini-Visual (Two Bars) -->
          <div v-if="rec.evidence.type === 'comparison'" class="mini-comparison-visual">
            <div class="mini-bar-row">
              <span class="mini-bar-label">{{ rec.evidence.prior.label }}</span>
              <div class="mini-bar-track">
                <div
                  class="mini-bar-fill prior"
                  :style="{ width: comparisonBarWidth(rec.evidence.prior.value, rec.evidence) }"
                ></div>
              </div>
              <span class="mini-bar-value">{{ formatConcisePeso(rec.evidence.prior.value) }}</span>
            </div>
            <div class="mini-bar-row">
              <span class="mini-bar-label">{{ rec.evidence.current.label }}</span>
              <div class="mini-bar-track">
                <div
                  class="mini-bar-fill"
                  :class="`fill-${rec.kind}`"
                  :style="{ width: comparisonBarWidth(rec.evidence.current.value, rec.evidence) }"
                ></div>
              </div>
              <span class="mini-bar-value current-val">{{ formatConcisePeso(rec.evidence.current.value) }}</span>
            </div>
          </div>

          <!-- Range Mini-Visual (Bracket / Line track with dot) -->
          <div v-else-if="rec.evidence.type === 'range'" class="mini-range-visual">
            <div class="range-track-wrapper">
              <div class="range-cap left"></div>
              <div class="range-line"></div>
              <div class="range-cap right"></div>
              <div class="range-dot" :style="{ left: rangeDotPosition(rec.evidence) }"></div>
            </div>
            <div class="range-labels-row">
              <span class="range-lbl-lower">{{ formatConcisePeso(rec.evidence.lower) }}</span>
              <span class="range-lbl-current" :style="{ left: rangeDotPosition(rec.evidence) }">
                {{ formatConcisePeso(rec.evidence.value) }}
              </span>
              <span class="range-lbl-upper">{{ formatConcisePeso(rec.evidence.upper) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue';
import { api, type ForecastData } from '../api';
import { useContentWidth } from '../composables/useContentWidth';
import {
  buildOverviewChartModel,
  buildForecastDecisions,
  formatConcisePeso,
  type ComparisonEvidence,
  type RangeEvidence,
} from '../forecastChart';
import { peso } from '../format';

export interface ForecastOverviewCardProps {
  data: ForecastData;
  showRecommendations?: boolean;
  showForecastLink?: boolean;
  title?: string;
  idPrefix?: string;
}

const props = withDefaults(defineProps<ForecastOverviewCardProps>(), {
  showRecommendations: true,
  showForecastLink: false,
  title: 'Overview',
  idPrefix: 'foc',
});

const chartBox = ref<HTMLElement | null>(null);
const { width: chartWidth, attach: attachChart } = useContentWidth(chartBox, 760);

// Display-only recorded history months toggle (defaults to 6 on mobile, 12 on desktop)
const historyMonths = ref<6 | 12>(typeof window !== 'undefined' && window.innerWidth < 760 ? 6 : 12);
const isNarrow = computed(() => (chartWidth.value || 800) < 760);

// Recommendation interactive selection
const selectedRecId = ref<string | null>(null);

// Interactive month hover / focus / tap
const activeMonthIndex = ref<number>(-1);

// Optional safe expense data matched to calendar months
interface MonthExpenses {
  total: number;
  categories: { name: string; amount: number }[];
}
const expenseMap = ref<Record<string, MonthExpenses>>({});

function setHistoryMonths(count: 6 | 12) {
  historyMonths.value = count;
  clearActiveMonth();
}

function toggleRec(recId: string) {
  clearActiveMonth();
  selectedRecId.value = selectedRecId.value === recId ? null : recId;
}

const recommendations = computed(() => {
  const d = props.data;
  if (!d?.ok || !d.steps?.length) return [];
  return buildForecastDecisions({ steps: d.steps, history: d.history });
});

const selectedRec = computed(() => {
  if (!selectedRecId.value) return null;
  return recommendations.value.find((r) => r.id === selectedRecId.value) ?? null;
});

const chartModel = computed(() => {
  const d = props.data;
  if (!d?.ok || !d.history?.length || !d.steps?.length) return null;
  return buildOverviewChartModel({
    width: chartWidth.value,
    history: d.history,
    steps: d.steps,
    dataSource: d.dataSource,
    historyMonths: historyMonths.value,
    selectedRecStepNumbers: selectedRec.value?.stepNumbers,
    recKind: selectedRec.value?.kind,
  });
});

const activeEntry = computed(() => {
  if (activeMonthIndex.value < 0 || !chartModel.value) return null;
  return chartModel.value.entries[activeMonthIndex.value] ?? null;
});

const activeEntryExpenses = computed(() => {
  if (!activeEntry.value || activeEntry.value.isForecast) return null;
  const parsedMonth = activeEntry.value.label.match(/^([A-Za-z]+)\s+(\d{4})$/);
  if (!parsedMonth) return null;
  const monthNames = [
    'january', 'february', 'march', 'april', 'may', 'june',
    'july', 'august', 'september', 'october', 'november', 'december'
  ];
  const mIdx = monthNames.indexOf(parsedMonth[1].toLowerCase());
  if (mIdx < 0) return null;
  const ym = `${parsedMonth[2]}-${String(mIdx + 1).padStart(2, '0')}`;
  return expenseMap.value[ym] ?? null;
});

function setActiveMonth(index: number) {
  activeMonthIndex.value = index;
}

function clearActiveMonth() {
  activeMonthIndex.value = -1;
}

function onTargetsFocusOut(event: FocusEvent) {
  const target = event.currentTarget as HTMLElement | null;
  if (!target || !target.contains(event.relatedTarget as Node)) {
    clearActiveMonth();
  }
}

async function focusMonth(index: number) {
  setActiveMonth(index);
  await nextTick();
  chartBox.value?.querySelectorAll<SVGElement>('.month-target')[index]?.focus();
}

function handleMonthKeydown(event: KeyboardEvent, currentIndex: number) {
  if (!chartModel.value) return;
  const total = chartModel.value.entries.length;
  if (event.key === 'ArrowLeft') {
    event.preventDefault();
    void focusMonth(Math.max(0, currentIndex - 1));
  } else if (event.key === 'ArrowRight') {
    event.preventDefault();
    void focusMonth(Math.min(total - 1, currentIndex + 1));
  } else if (event.key === 'Home') {
    event.preventDefault();
    void focusMonth(0);
  } else if (event.key === 'End') {
    event.preventDefault();
    void focusMonth(total - 1);
  } else if (event.key === 'Escape') {
    event.preventDefault();
    clearActiveMonth();
  }
}

function monthAriaLabel(entry: { fullDate: string; isForecast: boolean }) {
  return `${entry.fullDate}, ${entry.isForecast ? 'forecast' : 'recorded'} revenue, show monthly details`;
}

function hitBounds(idx: number) {
  if (!chartModel.value) return { left: 0, width: 40 };
  const entries = chartModel.value.entries;
  const stepX = (chartModel.value.plotRight - chartModel.value.plotLeft - 12) / Math.max(entries.length - 1, 1);
  const half = stepX / 2;
  const entry = entries[idx];
  const left = Math.max(chartModel.value.plotLeft, entry.x - half);
  const right = Math.min(chartModel.value.plotRight, entry.x + half);
  return { left, width: Math.max(right - left, 12) };
}

const tooltipWidth = computed(() => {
  const available = (chartWidth.value || 320) - 24;
  return isNarrow.value ? Math.min(250, available) : 250;
});

const tooltipHeight = computed(() => {
  if (!activeEntry.value) return 80;
  if (activeEntry.value.isForecast) return 118;
  if (activeEntryExpenses.value) {
    const cats = activeEntryExpenses.value.categories.length;
    return 108 + cats * 23;
  }
  return 72;
});

const tooltipX = computed(() => {
  if (!activeEntry.value || !chartModel.value) return 0;
  const x = activeEntry.value.x;
  const w = tooltipWidth.value;
  const margin = 8;
  const leftLimit = margin;
  const rightLimit = chartModel.value.width - margin;

  let ideal = x - w / 2;
  if (ideal + w > rightLimit) {
    ideal = rightLimit - w;
  }
  if (ideal < leftLimit) {
    ideal = leftLimit;
  }
  return ideal;
});

const tooltipY = computed(() => {
  if (!activeEntry.value || !chartModel.value) return 0;
  const h = tooltipHeight.value;
  const targetY = activeEntry.value.y;
  const above = targetY - h - 14;
  if (above >= chartModel.value.plotTop + 4) {
    return above;
  }
  const below = targetY + 16;
  if (below + h <= chartModel.value.plotBottom - 4) {
    return below;
  }
  return chartModel.value.plotTop + 8;
});

function comparisonBarWidth(val: number, ev: ComparisonEvidence) {
  const max = Math.max(ev.prior.value, ev.current.value, 1);
  const pct = Math.max(4, Math.round((val / max) * 100));
  return `${pct}%`;
}

function rangeDotPosition(ev: RangeEvidence) {
  const span = Math.max(ev.upper - ev.lower, 1);
  const pos = Math.max(0, Math.min(100, Math.round(((ev.value - ev.lower) / span) * 100)));
  return `${pos}%`;
}

const sourceNote = computed(() => {
  const d = props.data;
  if (!d) return '';
  const s = d.dataSource;
  if (s.usingLiveRecords) {
    const gaps = s.filledMonths > 0
      ? ` (${s.filledMonths} month${s.filledMonths === 1 ? '' : 's'} had no bookings and counted as zero)`
      : '';
    return `Based on recorded booking revenue — ${s.liveMonthsAvailable} months recorded${gaps}.`;
  }
  if (s.usingSample) {
    return 'Demonstration data, not a live forecast.';
  }
  return 'Live booking revenue forecast.';
});

const modelInfoNote = computed(() => {
  return 'Holt-Winters triple exponential smoothing · 6-month outlook (₱) · ~80% prediction interval';
});

async function loadSafeExpenses() {
  if (!props.data?.ok || !props.data.dataSource?.usingLiveRecords) {
    expenseMap.value = {};
    return;
  }
  try {
    const rows = await api.expenses();
    const active = rows.filter((r) => !r.voided);
    const byMonth: Record<string, { total: number; categories: Record<string, number> }> = {};
    for (const r of active) {
      if (!r.date) continue;
      const ym = r.date.slice(0, 7);
      if (!byMonth[ym]) byMonth[ym] = { total: 0, categories: {} };
      const amt = Number(r.amount) || 0;
      byMonth[ym].total += amt;
      const cat = r.category || 'Other';
      byMonth[ym].categories[cat] = (byMonth[ym].categories[cat] || 0) + amt;
    }
    const map: Record<string, MonthExpenses> = {};
    for (const [ym, d] of Object.entries(byMonth)) {
      const topCats = Object.entries(d.categories)
        .map(([name, amount]) => ({ name, amount }))
        .sort((a, b) => b.amount - a.amount)
        .slice(0, 3);
      map[ym] = { total: d.total, categories: topCats };
    }
    expenseMap.value = map;
  } catch {
    expenseMap.value = {};
  }
}

onMounted(async () => {
  await nextTick();
  attachChart();
  if (props.data?.ok && props.data.dataSource?.usingLiveRecords) {
    loadSafeExpenses();
  }
});
</script>

<style scoped>
/* ============================================================
   Overview Tool (Genuinely framed tool, radius 8px)
   ============================================================ */
.overview-tool {
  background: #ffffff;
  border: 1px solid #aab9c8;
  border-radius: 8px;
  overflow: hidden;
  margin-bottom: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.overview-chart-section {
  padding: 24px 24px 16px;
  background: #ffffff;
}

.overview-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.overview-title-group {
  display: flex;
  align-items: center;
  gap: 12px;
}

.overview-title {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  color: #26323d;
  letter-spacing: 0;
}

.overview-header-actions {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.view-full-link {
  font-size: 13.5px;
  font-weight: 600;
  color: #13796c;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  text-decoration: none;
}

.view-full-link:hover {
  text-decoration: underline;
  color: #0e5b52;
}

/* History Segmented Toggle Controls */
.history-toggle-group {
  display: inline-flex;
  border: 1px solid #d9dfe5;
  border-radius: 6px;
  background: #ffffff;
  padding: 2px;
  gap: 2px;
}

.history-toggle-btn {
  min-height: 40px;
  padding: 0 16px;
  border: none;
  background: transparent;
  font-size: 14px;
  font-weight: 400;
  color: #57616d;
  cursor: pointer;
  border-radius: 4px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: background 0.15s ease, color 0.15s ease;
  letter-spacing: 0;
}

.history-toggle-btn:hover {
  background: #f0f4f8;
}

.history-toggle-btn.active {
  background: #eaf0f7;
  color: #233e69;
  font-weight: 600;
}

.history-toggle-btn:focus-visible {
  outline: 2px solid #233e69;
  outline-offset: 1px;
}

/* Legend Row */
.overview-legend-row {
  display: flex;
  align-items: center;
  gap: 24px;
  flex-wrap: wrap;
  margin: 12px 0 16px;
  font-size: 13.5px;
  color: #26323d;
  letter-spacing: 0;
}

.legend-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.legend-swatch-band {
  display: inline-block;
  width: 22px;
  height: 14px;
  background: #dcece7;
  border: 1px solid #a9c9be;
  border-radius: 2px;
}

/* Plot Wrapper & SVG */
.overview-plot-wrapper {
  position: relative;
  width: 100%;
}

.overview-svg {
  display: block;
  width: 100%;
  height: auto;
  user-select: none;
  letter-spacing: 0;
}

.month-target {
  cursor: crosshair;
  outline: none;
}

.month-target:focus-visible .month-hit-rect {
  fill: rgba(35, 62, 105, 0.08);
}

/* Footer Provenance Meta */
.overview-footer-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 14px;
  padding-top: 10px;
  border-top: 1px solid #eef2f5;
  font-size: 12.5px;
  color: #57616d;
  letter-spacing: 0;
}

/* ============================================================
   Recommendations Area (Lightly tinted green #edf3ef)
   ============================================================ */
.overview-recommendations-section {
  background: #edf3ef;
  border-top: 1px solid #c7d4ce;
  padding: 4px 0;
}

.rec-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
  padding: 20px 24px;
  cursor: pointer;
  transition: background 0.15s ease;
  border-bottom: 1px solid #c7d4ce;
  border-left: 4px solid transparent;
  min-height: 44px;
  letter-spacing: 0;
}

.rec-row:last-child {
  border-bottom: none;
}

.rec-row:hover {
  background: #e1ece5;
}

.rec-row.selected {
  background: #dce9e2;
  border-left-color: #13796c;
}

.rec-row:focus-visible {
  outline: 2px solid #456a60;
  outline-offset: -2px;
}

.rec-main-col {
  flex: 1;
  max-width: 58%;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.rec-action-heading {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #26323d;
  line-height: 1.3;
  letter-spacing: 0;
}

.rec-finding-line {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  letter-spacing: 0;
}

.rec-triangle-svg {
  flex: none;
  width: 16px;
  height: 14px;
}

.finding-kind-increase {
  color: #126c5f;
}

.finding-kind-uncertainty {
  color: #8c5b0a;
}

.finding-kind-decrease {
  color: #a33d41;
}

/* Evidence Mini-Visual Column */
.rec-evidence-col {
  width: 38%;
  min-width: 250px;
  max-width: 360px;
  flex-shrink: 0;
}

/* Comparison Bars */
.mini-comparison-visual {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.mini-bar-row {
  display: grid;
  grid-template-columns: 36px 1fr 68px;
  align-items: center;
  gap: 10px;
  font-size: 13.5px;
}

.mini-bar-label {
  color: #57616d;
  font-variant-numeric: tabular-nums;
}

.mini-bar-track {
  width: 100%;
  height: 12px;
  background: transparent;
  display: flex;
  align-items: center;
}

.mini-bar-fill {
  height: 12px;
  border-radius: 2px;
}

.mini-bar-fill.prior {
  background: #b5c3cf;
}

.mini-bar-fill.fill-increase {
  background: #126c5f;
}

.mini-bar-fill.fill-decrease {
  background: #a33d41;
}

.mini-bar-value {
  text-align: right;
  color: #485664;
  font-variant-numeric: tabular-nums;
}

.mini-bar-value.current-val {
  font-weight: 600;
  color: #26323d;
}

/* Range Track */
.mini-range-visual {
  display: flex;
  flex-direction: column;
  width: 100%;
  gap: 4px;
}

.range-track-wrapper {
  position: relative;
  height: 24px;
  display: flex;
  align-items: center;
}

.range-line {
  width: 100%;
  height: 3px;
  background: #8c5b0a;
}

.range-cap {
  position: absolute;
  top: 4px;
  bottom: 4px;
  width: 2px;
  background: #8c5b0a;
}

.range-cap.left {
  left: 0;
}

.range-cap.right {
  right: 0;
}

.range-dot {
  position: absolute;
  top: 50%;
  width: 9px;
  height: 9px;
  background: #8c5b0a;
  border-radius: 50%;
  transform: translate(-50%, -50%);
}

.range-labels-row {
  position: relative;
  display: flex;
  justify-content: space-between;
  font-size: 12.5px;
  color: #57616d;
  font-variant-numeric: tabular-nums;
}

.range-lbl-current {
  position: absolute;
  transform: translateX(-50%);
  font-weight: 600;
  color: #26323d;
}

/* ============================================================
   Mobile Stacking (< 760px)
   ============================================================ */
@media (max-width: 760px) {
  .overview-chart-section {
    padding: 16px 14px 12px;
  }

  .overview-header-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .overview-header-actions {
    width: 100%;
    justify-content: space-between;
  }

  .history-toggle-group {
    width: 100%;
    display: flex;
  }

  .history-toggle-btn {
    flex: 1;
    min-height: 44px;
  }

  .overview-legend-row {
    gap: 12px 16px;
    margin: 8px 0 12px;
    font-size: 12.5px;
  }

  .rec-row {
    flex-direction: column;
    align-items: flex-start;
    padding: 16px 14px;
    gap: 14px;
  }

  .rec-main-col {
    max-width: 100%;
    width: 100%;
  }

  .rec-action-heading {
    font-size: 18px;
  }

  .rec-evidence-col {
    width: 100%;
    max-width: 100%;
    min-width: 0;
  }

  .overview-footer-meta {
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
    font-size: 12px;
  }
}
</style>
