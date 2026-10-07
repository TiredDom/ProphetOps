export interface ForecastHistoryPoint {
  label: string;
  month: string;
  value: number;
}

export interface ForecastStepPoint {
  step: number;
  month: string;
  monthLabel: string;
  value: number;
  lower: number;
  upper: number;
}

export interface ForecastDataSourceShape {
  lastRecordedMonth: string | null;
}

export interface TimelinePoint {
  kind: 'history' | 'forecast';
  index: number;
  label: string;
  shortLabel: string;
  value: number;
  lower?: number;
  upper?: number;
  step?: number;
}

export interface ForecastChartModel {
  width: number;
  height: number;
  plotLeft: number;
  plotRight: number;
  plotTop: number;
  plotBottom: number;
  labelY: number;
  baselineY: number;
  ticks: { value: number; y: number; label: string }[];
  timeline: TimelinePoint[];
  historyPoints: string;
  forecastPoints: string;
  band: string;
  divider: number;
  transitionLabel: string;
  historyMarkers: { x: number; y: number; label: string }[];
  forecastMarkers: { x: number; y: number; step: number; label: string }[];
  xLabels: { index: number; x: number; text: string; anchor: 'start' | 'middle' | 'end' }[];
}

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

export function formatCompactPeso(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1_000_000) return `₱${(value / 1_000_000).toFixed(value % 1_000_000 === 0 ? 0 : 1)}M`;
  if (abs >= 1_000) return `₱${Math.round(value / 1_000)}k`;
  return `₱${Math.round(value)}`;
}

export function formatConcisePeso(value: number): string {
  if (!Number.isFinite(value)) return '₱0';
  const abs = Math.abs(value);
  if (abs >= 1_000_000) return `₱${(value / 1_000_000).toFixed(2)}M`;
  if (abs >= 1_000) return `₱${Math.round(value / 1_000)}K`;
  return `₱${Math.round(value)}`;
}

function niceNum(range: number, round: boolean): number {
  const safeRange = Number.isFinite(range) && range > 0 ? range : 1;
  const exponent = Math.floor(Math.log10(safeRange));
  const fraction = safeRange / Math.pow(10, exponent);
  let niceFraction: number;
  if (round) {
    if (fraction < 1.5) niceFraction = 1;
    else if (fraction < 3) niceFraction = 2;
    else if (fraction < 7) niceFraction = 5;
    else niceFraction = 10;
  } else {
    if (fraction <= 1) niceFraction = 1;
    else if (fraction <= 2) niceFraction = 2;
    else if (fraction <= 5) niceFraction = 5;
    else niceFraction = 10;
  }
  return niceFraction * Math.pow(10, exponent);
}

function parseMonthYear(label: string | null | undefined): { monthIndex: number; year: number } | null {
  if (!label) return null;
  const match = label.trim().match(/^([A-Za-z]+)\s+(\d{4})$/);
  if (!match) return null;
  const monthIndex = MONTHS.findIndex(
    (month) => month.toLowerCase() === match[1].toLowerCase() || month.toLowerCase().startsWith(match[1].toLowerCase()),
  );
  const year = Number(match[2]);
  if (monthIndex < 0 || !Number.isInteger(year)) return null;
  return { monthIndex, year };
}

function extractYear(label?: string | null, shortLabel?: string | null): number | null {
  const parsed = parseMonthYear(label);
  if (parsed) return parsed.year;

  const raw = `${label ?? ''} ${shortLabel ?? ''}`.trim();
  const fourDigitMatch = raw.match(/\b(19\d\d|20\d\d)\b/);
  if (fourDigitMatch) {
    const yr = Number(fourDigitMatch[1]);
    if (Number.isInteger(yr)) return yr;
  }

  const twoDigitMatch = raw.match(/\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s*['’]?(\d{2})\b/i);
  if (twoDigitMatch) {
    const yr = 2000 + Number(twoDigitMatch[1]);
    if (Number.isInteger(yr)) return yr;
  }

  return null;
}

function addMonths(base: { monthIndex: number; year: number }, offset: number) {
  const total = base.year * 12 + base.monthIndex + offset;
  return {
    monthIndex: ((total % 12) + 12) % 12,
    year: Math.floor(total / 12),
  };
}

function formatMonthYear(date: { monthIndex: number; year: number }) {
  return `${MONTHS[date.monthIndex]} ${date.year}`;
}

function shortMonthYear(label: string) {
  const parsed = parseMonthYear(label);
  if (!parsed) return label;
  return `${MONTHS[parsed.monthIndex].slice(0, 3)} ${String(parsed.year).slice(2)}`;
}

function stepFallbackLabel(step: ForecastStepPoint) {
  return step.monthLabel || step.month || `Step ${step.step}`;
}

function labelBounds(label: { x: number; text: string; anchor: 'start' | 'middle' | 'end' }) {
  const estimatedWidth = label.text.length * 6.4;
  if (label.anchor === 'start') return { left: label.x, right: label.x + estimatedWidth };
  if (label.anchor === 'end') return { left: label.x - estimatedWidth, right: label.x };
  return { left: label.x - estimatedWidth / 2, right: label.x + estimatedWidth / 2 };
}

function labelsOverlap(
  label: { x: number; text: string; anchor: 'start' | 'middle' | 'end' },
  accepted: { x: number; text: string; anchor: 'start' | 'middle' | 'end' }[],
) {
  const gap = 8;
  const bounds = labelBounds(label);
  return accepted.some((other) => {
    const otherBounds = labelBounds(other);
    return bounds.left < otherBounds.right + gap && bounds.right + gap > otherBounds.left;
  });
}

export function buildForecastTimeline(
  history: ForecastHistoryPoint[],
  steps: ForecastStepPoint[],
  dataSource: ForecastDataSourceShape,
): TimelinePoint[] {
  const lastActual = parseMonthYear(dataSource.lastRecordedMonth);
  const firstForecast = parseMonthYear(steps[0]?.monthLabel);
  const firstForecastOffset = steps[0] ? Math.max(steps[0].step - 1, 0) : 0;

  const historyBase = lastActual
    ? addMonths(lastActual, -(history.length - 1))
    : firstForecast
      ? addMonths(firstForecast, -(history.length + firstForecastOffset))
      : null;

  const actuals: TimelinePoint[] = history.map((point, index) => {
    const label = historyBase ? formatMonthYear(addMonths(historyBase, index)) : point.month || point.label;
    return {
      kind: 'history' as const,
      index,
      label,
      shortLabel: shortMonthYear(label),
      value: point.value,
    };
  });

  const forecasts: TimelinePoint[] = steps.map((step, stepIndex) => {
    const parsedMonth = parseMonthYear(step.monthLabel);
    const label = parsedMonth ? formatMonthYear(parsedMonth) : stepFallbackLabel(step);
    return {
      kind: 'forecast' as const,
      index: history.length + stepIndex,
      label,
      shortLabel: shortMonthYear(label),
      value: step.value,
      lower: step.lower,
      upper: step.upper,
      step: step.step,
    };
  });

  return [...actuals, ...forecasts];
}

export function buildForecastChartModel(params: {
  width: number;
  history: ForecastHistoryPoint[];
  steps: ForecastStepPoint[];
  dataSource: ForecastDataSourceShape;
}): ForecastChartModel | null {
  const { history, steps, dataSource } = params;
  if (!history.length || !steps.length) return null;

  const width = Math.max(220, Math.round(params.width || 320));
  const height = width < 520 ? 240 : 280;
  const plotLeft = width < 300 ? 44 : width < 420 ? 52 : 62;
  const plotRight = width - (width < 300 ? 10 : 18);
  const plotTop = 18;
  const plotBottom = height - 42;
  const labelY = plotBottom + 22;
  const timeline = buildForecastTimeline(history, steps, dataSource);
  const values = timeline.flatMap((point) => [point.value, point.lower ?? point.value, point.upper ?? point.value]);
  const finiteValues = values.filter((value) => Number.isFinite(value));
  if (!finiteValues.length) return null;

  const rawMin = Math.min(...finiteValues);
  const rawMax = Math.max(...finiteValues);
  const spread = niceNum(Math.max(rawMax - rawMin, 1), false);
  const tickStep = niceNum(spread / 4, true);
  const niceMin = Math.floor(rawMin / tickStep) * tickStep;
  const niceMax = Math.ceil(rawMax / tickStep) * tickStep;
  const span = Math.max(niceMax - niceMin, 1);
  const stepX = (plotRight - plotLeft) / Math.max(timeline.length - 1, 1);
  const x = (index: number) => Number((plotLeft + stepX * index).toFixed(1));
  const y = (value: number) => Number((plotTop + ((niceMax - value) / span) * (plotBottom - plotTop)).toFixed(1));

  const ticks: { value: number; y: number; label: string }[] = [];
  for (let value = niceMin; value <= niceMax + tickStep / 2; value += tickStep) {
    ticks.push({ value, y: y(value), label: formatCompactPeso(value) });
  }

  const historyMarkers = history.map((point, index) => ({
    x: x(index),
    y: y(point.value),
    label: timeline[index].label,
  }));

  const forecastMarkers = steps.map((step, stepIndex) => {
    const timelineIndex = history.length + stepIndex;
    return {
      x: x(timelineIndex),
      y: y(step.value),
      step: step.step,
      label: timeline[timelineIndex].label,
    };
  });

  const historyPoints = historyMarkers.map((point) => `${point.x},${point.y}`).join(' ');
  const forecastPoints = forecastMarkers.map((point) => `${point.x},${point.y}`).join(' ');
  const bandTop = steps.map((step, stepIndex) => `${x(history.length + stepIndex)},${y(step.upper)}`);
  const bandBottom = steps.map((step, stepIndex) => `${x(history.length + stepIndex)},${y(step.lower)}`).reverse();
  const band = bandTop.concat(bandBottom).join(' ');
  const lastHistoryX = x(history.length - 1);
  const firstForecastX = x(history.length);
  const divider = Number(((lastHistoryX + firstForecastX) / 2).toFixed(1));
  const transitionLabel = `Recorded through ${timeline[history.length - 1].label}; forecast starts ${timeline[history.length].label}.`;

  const candidateIndexes = Array.from({ length: timeline.length }, (_, index) => index);
  const rankedLabels = candidateIndexes
    .map((index) => ({
      index,
      x: x(index),
      text: timeline[index].shortLabel,
      anchor: index === 0 ? 'start' as const : index === timeline.length - 1 ? 'end' as const : 'middle' as const,
      priority: index === 0 || index === timeline.length - 1
        ? 0
        : index === history.length - 1 || index === history.length
          ? 1
          : index % (width < 520 ? 4 : 3) === 0
            ? 2
            : 3,
    }))
    .sort((a, b) => a.priority - b.priority || Math.abs(a.index - history.length) - Math.abs(b.index - history.length));

  const acceptedLabels: { index: number; x: number; text: string; anchor: 'start' | 'middle' | 'end' }[] = [];
  for (const candidate of rankedLabels) {
    const { priority, ...label } = candidate;
    if (priority > 2 && width < 760) continue;
    if (!labelsOverlap(label, acceptedLabels)) acceptedLabels.push(label);
  }
  const xLabels = acceptedLabels.sort((a, b) => a.index - b.index);

  return {
    width,
    height,
    plotLeft,
    plotRight,
    plotTop,
    plotBottom,
    labelY,
    baselineY: y(niceMin),
    ticks,
    timeline,
    historyPoints,
    forecastPoints,
    band,
    divider,
    transitionLabel,
    historyMarkers,
    forecastMarkers,
    xLabels,
  };
}

export type RecommendationKind = 'increase' | 'uncertainty' | 'decrease';

export interface ComparisonEvidence {
  type: 'comparison';
  prior: {
    label: string;
    value: number;
  };
  current: {
    label: string;
    value: number;
  };
  changePercent: number;
}

export interface RangeEvidence {
  type: 'range';
  label: string;
  lower: number;
  value: number;
  upper: number;
}

export type RecommendationEvidence = ComparisonEvidence | RangeEvidence;

export interface DecisionRecommendation {
  id: string;
  action: string;
  finding: string;
  kind: RecommendationKind;
  stepNumbers: number[];
  evidence: RecommendationEvidence;
}

export interface OverviewChartEntry {
  index: number;
  x: number;
  y: number;
  value: number;
  isForecast: boolean;
  forecastStepNumber?: number;
  forecastIndex?: number;
  historyIndex?: number;
  label: string;
  shortMonth: string;
  year: number | null;
  fullDate: string;
  lower?: number;
  upper?: number;
}

export interface OverviewChartModel {
  width: number;
  height: number;
  plotLeft: number;
  plotRight: number;
  plotTop: number;
  plotBottom: number;
  plotWidth: number;
  plotHeight: number;
  dividerX: number;
  entries: OverviewChartEntry[];
  yTicks: { value: number; y: number; label: string }[];
  xTicks: { index: number; x: number; shortMonth: string; year: number | null }[];
  historyPath: string;
  forecastPath: string;
  bandPath: string;
  historyLength: number;
  highlightRegion?: {
    x: number;
    width: number;
    color: string;
    points: { x: number; y: number }[];
  } | null;
}

export interface BuildOverviewChartParams {
  width: number;
  history: ForecastHistoryPoint[];
  steps: ForecastStepPoint[];
  dataSource: ForecastDataSourceShape;
  historyMonths?: 6 | 12;
  selectedRecStepNumbers?: number[];
  recKind?: RecommendationKind | null;
}

function extractMonthName(step: { monthLabel?: string; month?: string; step?: number }): string {
  if (step.monthLabel) {
    const parts = step.monthLabel.trim().split(/\s+/);
    return parts[0];
  }
  if (step.month) return step.month;
  return `Step ${step.step ?? 1}`;
}

function extractShortMonth(step: { monthLabel?: string; month?: string; step?: number }): string {
  return extractMonthName(step).slice(0, 3);
}

function toSvgPath(points: [number, number][]): string {
  return points
    .map(([px, py], i) => `${i ? 'L' : 'M'}${px.toFixed(2)} ${py.toFixed(2)}`)
    .join(' ');
}

export function buildForecastDecisions(params: {
  steps: ForecastStepPoint[];
  history?: ForecastHistoryPoint[];
}): DecisionRecommendation[] {
  const { steps, history } = params;
  if (!Array.isArray(steps) || steps.length === 0) return [];

  const validSteps = steps.filter(
    (s) => s && typeof s === 'object' && Number.isFinite(s.value) && Number.isFinite(s.step),
  );
  if (!validSteps.length) return [];

  const lastHistory = history && history.length > 0 ? history[history.length - 1] : null;
  const results: DecisionRecommendation[] = [];

  const maxVal = Math.max(...validSteps.map((s) => s.value));
  const minVal = Math.min(...validSteps.map((s) => s.value));
  const isFlat = Math.abs(maxVal - minVal) < 1e-4;

  let peakStep: ForecastStepPoint | null = null;
  let peakIndex = -1;
  if (!isFlat) {
    peakIndex = validSteps.findIndex((s) => s.value === maxVal);
    if (peakIndex >= 0) {
      peakStep = validSteps[peakIndex];
    }
  }

  if (peakStep) {
    let prior: { label: string; short: string; value: number; step?: number } | null = null;
    if (peakIndex > 0) {
      const prevStep = validSteps[peakIndex - 1];
      prior = {
        label: extractMonthName(prevStep),
        short: extractShortMonth(prevStep),
        value: prevStep.value,
        step: prevStep.step,
      };
    } else if (lastHistory && Number.isFinite(lastHistory.value) && lastHistory.value > 0) {
      prior = {
        label: lastHistory.month || lastHistory.label || 'prior month',
        short: (lastHistory.month || lastHistory.label || 'Prior').slice(0, 3),
        value: lastHistory.value,
      };
    }

    if (prior && Number.isFinite(prior.value) && prior.value > 0) {
      const diffPercent = ((peakStep.value - prior.value) / prior.value) * 100;
      if (diffPercent >= 0.5) {
        const peakMonth = extractMonthName(peakStep);
        const diffStr = `+${diffPercent.toFixed(1)}%`;
        results.push({
          id: 'peak',
          action: 'Review expenses before approving new spending',
          finding: `${peakMonth} peak / ${diffStr} versus ${prior.label}`,
          kind: 'increase',
          stepNumbers: prior.step !== undefined ? [prior.step, peakStep.step] : [peakStep.step],
          evidence: {
            type: 'comparison',
            prior: { label: prior.short, value: prior.value },
            current: { label: extractShortMonth(peakStep), value: peakStep.value },
            changePercent: Number(diffPercent.toFixed(1)),
          },
        });
      }
    }
  }

  let rangeCandidate: ForecastStepPoint | null = null;
  const stepsWithBounds = validSteps.filter((s) => Number.isFinite(s.lower) && Number.isFinite(s.upper));

  if (stepsWithBounds.length > 0) {
    if (peakStep && Number.isFinite(peakStep.lower) && Number.isFinite(peakStep.upper)) {
      rangeCandidate = peakStep;
    } else {
      let maxSpread = -1;
      for (const s of stepsWithBounds) {
        const spread = Math.abs(s.upper - s.lower);
        if (spread > maxSpread) {
          maxSpread = spread;
          rangeCandidate = s;
        }
      }
    }
  }

  if (rangeCandidate) {
    const rawLower = rangeCandidate.lower;
    const rawUpper = rangeCandidate.upper;
    const lower = Math.min(rawLower, rawUpper);
    const upper = Math.max(rawLower, rawUpper);
    const month = extractMonthName(rangeCandidate);

    results.push({
      id: 'range',
      action: `Keep ${month} commitments flexible`,
      finding: `${month} uncertainty / approx. 80% range`,
      kind: 'uncertainty',
      stepNumbers: [rangeCandidate.step],
      evidence: {
        type: 'range',
        label: extractShortMonth(rangeCandidate),
        lower,
        value: rangeCandidate.value,
        upper,
      },
    });
  }

  let dropPrior: ForecastStepPoint | null = null;
  let dropTarget: ForecastStepPoint | null = null;
  let maxDropPercent = 0;

  if (peakStep && peakIndex >= 0 && peakIndex < validSteps.length - 1) {
    const nextStep = validSteps[peakIndex + 1];
    if (peakStep.value > 0) {
      const dropPct = ((nextStep.value - peakStep.value) / peakStep.value) * 100;
      if (dropPct <= -0.5) {
        dropPrior = peakStep;
        dropTarget = nextStep;
        maxDropPercent = dropPct;
      }
    }
  }

  if (!dropTarget) {
    for (let i = 1; i < validSteps.length; i++) {
      const prev = validSteps[i - 1];
      const curr = validSteps[i];
      if (prev.value > 0) {
        const dropPct = ((curr.value - prev.value) / prev.value) * 100;
        if (dropPct <= -0.5 && dropPct < maxDropPercent) {
          maxDropPercent = dropPct;
          dropPrior = prev;
          dropTarget = curr;
        }
      }
    }
  }

  if (dropPrior && dropTarget) {
    const targetMonth = extractMonthName(dropTarget);
    const priorMonth = extractMonthName(dropPrior);
    const dropStr = `${maxDropPercent.toFixed(1)}%`;

    results.push({
      id: 'drop',
      action: `Review expense categories before ${targetMonth}`,
      finding: `${targetMonth} decrease / ${dropStr} versus ${priorMonth}`,
      kind: 'decrease',
      stepNumbers: [dropPrior.step, dropTarget.step],
      evidence: {
        type: 'comparison',
        prior: { label: extractShortMonth(dropPrior), value: dropPrior.value },
        current: { label: extractShortMonth(dropTarget), value: dropTarget.value },
        changePercent: Number(maxDropPercent.toFixed(1)),
      },
    });
  }

  if (results.length === 0) {
    const s1 = validSteps[0];
    const rawLower = Number.isFinite(s1.lower) ? s1.lower : s1.value;
    const rawUpper = Number.isFinite(s1.upper) ? s1.upper : s1.value;
    results.push({
      id: 'steady',
      action: 'Maintain baseline budget reviews',
      finding: 'Steady horizon / minimal projected variation',
      kind: 'uncertainty',
      stepNumbers: [s1.step],
      evidence: {
        type: 'range',
        label: extractShortMonth(s1),
        lower: Math.min(rawLower, rawUpper),
        value: s1.value,
        upper: Math.max(rawLower, rawUpper),
      },
    });
  }

  return results.slice(0, 3);
}

export function buildOverviewChartModel(params: BuildOverviewChartParams): OverviewChartModel | null {
  const { history, steps, dataSource } = params;
  if (!history.length || !steps.length) return null;

  const width = Math.max(260, Math.round(params.width || 320));
  const isNarrow = width < 760;
  const historyMonths = params.historyMonths ?? (isNarrow ? 6 : 12);
  const displayedHistory = history.slice(-historyMonths);
  if (!displayedHistory.length) return null;

  const timeline = buildForecastTimeline(displayedHistory, steps, dataSource);
  if (!timeline.length) return null;

  const allValues = timeline.flatMap((point) => [
    point.value,
    point.lower ?? point.value,
    point.upper ?? point.value,
  ]);
  const finiteValues = allValues.filter((v) => typeof v === 'number' && Number.isFinite(v));
  if (!finiteValues.length) return null;

  const rawMin = Math.min(...finiteValues);
  const rawMax = Math.max(...finiteValues);
  const niceMin = 0;
  const spread = niceNum(Math.max(rawMax - niceMin, 1), false);
  const tickStep = niceNum(spread / 4, true);
  const niceMax = Math.ceil(Math.max(rawMax, tickStep) / tickStep) * tickStep;
  const span = Math.max(niceMax - niceMin, 1);

  const plotTop = 28;
  const plotHeight = isNarrow ? 230 : 270;
  const plotBottom = plotTop + plotHeight;
  const plotLeft = isNarrow ? 56 : 72;
  const plotRight = width - (isNarrow ? 16 : 28);
  const plotWidth = plotRight - plotLeft;
  const height = plotBottom + 46;

  const totalPoints = timeline.length;
  const stepX = (plotRight - plotLeft - 12) / Math.max(totalPoints - 1, 1);
  const x = (i: number) => Number((plotLeft + 6 + i * stepX).toFixed(1));
  const y = (val: number) => Number((plotBottom - ((val - niceMin) / span) * plotHeight).toFixed(1));

  const yTicks: { value: number; y: number; label: string }[] = [];
  for (let val = niceMin; val <= niceMax + tickStep / 2; val += tickStep) {
    yTicks.push({ value: val, y: y(val), label: formatCompactPeso(val) });
  }

  const entries: OverviewChartEntry[] = timeline.map((point, index) => {
    const isForecast = point.kind === 'forecast';
    const parsed = parseMonthYear(point.label);
    const shortMonth = parsed ? MONTHS[parsed.monthIndex].slice(0, 3) : (point.shortLabel ? point.shortLabel.split(' ')[0] : (point.label || ''));
    const year = extractYear(point.label, point.shortLabel);
    return {
      index,
      x: x(index),
      y: y(point.value),
      value: point.value,
      isForecast,
      forecastStepNumber: point.step,
      forecastIndex: isForecast ? index - displayedHistory.length : undefined,
      historyIndex: !isForecast ? index : undefined,
      label: point.label,
      shortMonth,
      year,
      fullDate: point.label,
      lower: point.lower,
      upper: point.upper,
    };
  });

  const tickCount = isNarrow ? Math.max(3, Math.floor(plotWidth / 64)) : Math.max(5, Math.floor(plotWidth / 82));
  const tickIndexes = [...new Set(Array.from({ length: tickCount }, (_, i) => Math.round((i * (totalPoints - 1)) / (tickCount - 1))))];
  const xTicks = tickIndexes.map((idx) => ({
    index: idx,
    x: entries[idx].x,
    shortMonth: entries[idx].shortMonth,
    year: entries[idx].year,
  }));

  const historyLength = displayedHistory.length;
  const historyPath = toSvgPath(entries.slice(0, historyLength).map((e) => [e.x, e.y]));
  const forecastPath = toSvgPath(entries.slice(historyLength - 1).map((e) => [e.x, e.y]));

  const upperPts: [number, number][] = steps.map((s, i) => {
    const safeUpper = Number.isFinite(s.upper) && Number.isFinite(s.lower) ? Math.max(s.lower, s.upper) : s.value;
    return [entries[historyLength + i].x, y(safeUpper)];
  });
  const lowerPts: [number, number][] = steps
    .map((s, i) => {
      const safeLower = Number.isFinite(s.upper) && Number.isFinite(s.lower) ? Math.min(s.lower, s.upper) : s.value;
      return [entries[historyLength + i].x, y(safeLower)] as [number, number];
    })
    .reverse();
  const bandPath = `${toSvgPath(upperPts.concat(lowerPts))} Z`;

  const lastHistX = x(historyLength - 1);
  const firstFcX = x(historyLength);
  const dividerX = Number(((lastHistX + firstFcX) / 2).toFixed(1));

  let highlightRegion: OverviewChartModel['highlightRegion'] = null;
  if (params.selectedRecStepNumbers && params.selectedRecStepNumbers.length > 0) {
    const matched = entries.filter(
      (e) => e.isForecast && e.forecastStepNumber !== undefined && params.selectedRecStepNumbers!.includes(e.forecastStepNumber),
    );
    if (matched.length > 0) {
      const half = stepX / 2;
      const minX = Math.min(...matched.map((e) => e.x));
      const maxX = Math.max(...matched.map((e) => e.x));
      const left = Math.max(plotLeft, minX - half);
      const right = Math.min(plotRight, maxX + half);
      const color = params.recKind === 'increase' ? '#126c5f' : params.recKind === 'decrease' ? '#a33d41' : '#8c5b0a';
      highlightRegion = {
        x: left,
        width: right - left,
        color,
        points: matched.map((e) => ({ x: e.x, y: e.y })),
      };
    }
  }

  return {
    width,
    height,
    plotLeft,
    plotRight,
    plotTop,
    plotBottom,
    plotWidth,
    plotHeight,
    dividerX,
    entries,
    yTicks,
    xTicks,
    historyPath,
    forecastPath,
    bandPath,
    historyLength,
    highlightRegion,
  };
}
