// Plain facts read off a Holt-Winters forecast for the places outside the chart that show it:
// which month is which, the peak, the largest fall, and whether recent months are missing from
// the records. Nothing here forecasts anything; it only describes the forecast it is given.
import type { ForecastData, ForecastStepView } from './api';
import { formatConcisePeso } from './forecastChart';

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/** "October 2026" becomes "2026-10"; anything else gives null. */
export function monthKeyOfLabel(label: string | null | undefined): string | null {
  const match = /^([A-Za-z]+)\s+(\d{4})$/.exec((label ?? '').trim());
  if (!match) return null;
  const index = MONTHS.findIndex((name) => name.toLowerCase() === match[1].toLowerCase());
  return index < 0 ? null : `${match[2]}-${String(index + 1).padStart(2, '0')}`;
}

/** "2026-10" becomes "October 2026". */
export function labelOfMonthKey(key: string): string {
  const [year, month] = key.split('-').map(Number);
  return MONTHS[month - 1] ? `${MONTHS[month - 1]} ${year}` : key;
}

function shiftMonthKey(key: string, months: number): string {
  const [year, month] = key.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1 + months, 1));
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
}

/** The same month one year earlier: the seasonal match Holt-Winters itself leans on. */
export function sameMonthLastYear(key: string): { key: string; label: string; from: string; to: string } {
  const previous = shiftMonthKey(key, -12);
  const [year, month] = previous.split('-').map(Number);
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return {
    key: previous,
    label: labelOfMonthKey(previous),
    from: `${previous}-01`,
    to: `${previous}-${String(lastDay).padStart(2, '0')}`,
  };
}

export function monthName(step: ForecastStepView): string {
  return step.monthLabel.split(' ')[0] ?? step.monthLabel;
}

export function usableSteps(data: ForecastData | null | undefined): ForecastStepView[] {
  if (!data?.ok || !Array.isArray(data.steps)) return [];
  return data.steps.filter((s) => Number.isFinite(s.value));
}

export function stepForMonth(data: ForecastData | null | undefined, key: string): ForecastStepView | null {
  return usableSteps(data).find((s) => monthKeyOfLabel(s.monthLabel) === key) ?? null;
}

/** The highest month, or null when the six months are effectively level. */
export function peakStep(data: ForecastData | null | undefined): ForecastStepView | null {
  const steps = usableSteps(data);
  if (steps.length === 0) return null;
  const values = steps.map((s) => s.value);
  if (Math.max(...values) - Math.min(...values) < 1e-4) return null;
  return steps.reduce((best, s) => (s.value > best.value ? s : best), steps[0]);
}

/** The month straight after `step`, when it is at least half a percent lower. */
export function fallAfter(
  data: ForecastData | null | undefined,
  step: ForecastStepView,
): { step: ForecastStepView; percent: number } | null {
  const steps = usableSteps(data);
  const index = steps.findIndex((s) => s.step === step.step);
  const next = steps[index + 1];
  if (index < 0 || !next || step.value <= 0) return null;
  const percent = ((next.value - step.value) / step.value) * 100;
  return percent <= -0.5 ? { step: next, percent } : null;
}

export function forecastTotal(data: ForecastData | null | undefined): number {
  return usableSteps(data).reduce((sum, s) => sum + s.value, 0);
}

export function forecastSpan(data: ForecastData | null | undefined): string {
  const steps = usableSteps(data);
  return steps.length ? `${steps[0].monthLabel} – ${steps[steps.length - 1].monthLabel}` : '';
}

/**
 * Months between the last month with bookings and the last complete month. The forecast
 * starts after the last month with bookings, so these are months it is not using.
 */
export function unrecordedMonths(
  data: ForecastData | null | undefined,
  today: string,
): { first: string; last: string; count: number; lastRecorded: string } | null {
  const lastRecorded = monthKeyOfLabel(data?.dataSource?.lastRecordedMonth);
  if (!data?.ok || !lastRecorded || !/^\d{4}-\d{2}/.test(today)) return null;
  const lastComplete = shiftMonthKey(today.slice(0, 7), -1);
  if (lastRecorded >= lastComplete) return null;

  const first = shiftMonthKey(lastRecorded, 1);
  let count = 0;
  for (let key = first; key <= lastComplete; key = shiftMonthKey(key, 1)) count += 1;
  return {
    first: labelOfMonthKey(first),
    last: labelOfMonthKey(lastComplete),
    count,
    lastRecorded: labelOfMonthKey(lastRecorded),
  };
}

export function describeMonths(gap: { first: string; last: string; count: number }): string {
  if (gap.count === 1) return gap.first;
  const [firstMonth, firstYear] = gap.first.split(' ');
  const [lastMonth, lastYear] = gap.last.split(' ');
  return firstYear === lastYear ? `${firstMonth}–${lastMonth} ${lastYear}` : `${gap.first} – ${gap.last}`;
}

export interface HeadlinePart {
  text: string;
  strong?: boolean;
}

function percentText(value: number): string {
  return `${Math.abs(value).toFixed(1)}%`;
}

/** One sentence that says where the forecast goes, with the figures marked for emphasis. */
export function forecastHeadline(data: ForecastData | null | undefined): HeadlinePart[] {
  const steps = usableSteps(data);
  if (steps.length === 0) return [];
  const measure = data?.measure ?? 'revenue';
  const subject = measure === 'bookings' ? 'Bookings are' : measure === 'passengers' ? 'Passenger numbers are' : 'Revenue is';
  // Counts are forecast as averages such as 4.4, which reads better as "about 4".
  const figure = (value: number) =>
    measure === 'revenue' ? formatConcisePeso(value) : `about ${Math.round(value).toLocaleString('en-PH')}`;

  const first = steps[0];
  const last = steps[steps.length - 1];
  const peak = peakStep(data);

  if (!peak) {
    return [
      { text: `${subject} forecast to hold near ` },
      { text: figure(first.value), strong: true },
      { text: ` a month through ${last.monthLabel}.` },
    ];
  }

  const fall = fallAfter(data, peak);
  // The year is only repeated when the fall crosses into the next one.
  const peakYear = peak.monthLabel.split(' ')[1];
  const fallMonth = fall
    ? fall.step.monthLabel.split(' ')[1] === peakYear ? monthName(fall.step) : fall.step.monthLabel
    : '';

  if (peak.step === first.step) {
    return [
      { text: `${subject} forecast to be highest in ` },
      { text: `${peak.monthLabel} at ${figure(peak.value)}`, strong: true },
      { text: ', then ease to ' },
      { text: figure(last.value), strong: true },
      { text: ` by ${last.monthLabel}.` },
    ];
  }

  if (fall) {
    return [
      { text: `${subject} forecast to climb to a ` },
      { text: `${monthName(peak)} peak of ${figure(peak.value)}`, strong: true },
      { text: ', then fall ' },
      { text: percentText(fall.percent), strong: true },
      { text: ` in ${fallMonth}.` },
    ];
  }

  // "Keep rising" only when every month is at least the one before; a dip on the way is a peak.
  const rising = steps.every((s, i) => i === 0 || s.value >= steps[i - 1].value);
  if (peak.step === last.step && rising) {
    return [
      { text: `${subject} forecast to keep rising, reaching ` },
      { text: figure(peak.value), strong: true },
      { text: ` in ${peak.monthLabel}.` },
    ];
  }

  return [
    { text: `${subject} forecast to peak in ` },
    { text: `${peak.monthLabel} at ${figure(peak.value)}`, strong: true },
    { text: '.' },
  ];
}
