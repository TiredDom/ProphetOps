// Calendar periods for filtering records. Dates are business dates written yyyy-MM-dd, which
// compare correctly as strings, so no time zone is involved anywhere here.

export type PeriodKey = 'all' | 'month' | 'last-month' | 'quarter' | 'year' | 'custom';

export interface DateRange {
  /** Inclusive start, or '' for no lower bound. */
  from: string;
  /** Inclusive end, or '' for no upper bound. */
  to: string;
}

export const periodOptions: { key: PeriodKey; label: string }[] = [
  { key: 'all', label: 'Any time' },
  { key: 'month', label: 'This month' },
  { key: 'last-month', label: 'Last month' },
  { key: 'quarter', label: 'Last 3 months' },
  { key: 'year', label: 'This year' },
  { key: 'custom', label: 'Custom range' },
];

export function isPeriodKey(value: string): value is PeriodKey {
  return periodOptions.some((option) => option.key === value);
}

export function isIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [y, m, d] = parts(value);
  return m >= 1 && m <= 12 && d >= 1 && d <= daysIn(y, m);
}

function parts(iso: string): [number, number, number] {
  const [y, m, d] = iso.split('-').map(Number);
  return [y, m, d];
}

function daysIn(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

function iso(year: number, month: number, day: number): string {
  // Month arithmetic can run past either end of the year; Date.UTC folds it back.
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.toISOString().slice(0, 10);
}

function monthStart(year: number, month: number): string {
  return iso(year, month, 1);
}

function monthEnd(year: number, month: number): string {
  return iso(year, month + 1, 0);
}

/** The range a period covers, or null when it does not restrict anything. */
export function resolvePeriod(key: PeriodKey, today: string, from = '', to = ''): DateRange | null {
  if (key === 'custom') {
    const start = isIsoDate(from) ? from : '';
    const end = isIsoDate(to) ? to : '';
    return start || end ? { from: start, to: end } : null;
  }
  if (key === 'all' || !isIsoDate(today)) return null;

  const [y, m] = parts(today);
  switch (key) {
    case 'month':
      return { from: monthStart(y, m), to: monthEnd(y, m) };
    case 'last-month':
      return { from: monthStart(y, m - 1), to: monthEnd(y, m - 1) };
    case 'quarter':
      return { from: monthStart(y, m - 2), to: monthEnd(y, m) };
    case 'year':
      return { from: iso(y, 1, 1), to: iso(y, 12, 31) };
    default:
      return null;
  }
}

export function inRange(date: string, range: DateRange | null): boolean {
  if (!range) return true;
  if (range.from && date < range.from) return false;
  if (range.to && date > range.to) return false;
  return true;
}

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const fullMonthNames = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/** "Oct 13, 2026" */
export function formatDate(value: string): string {
  if (!isIsoDate(value)) return value;
  const [y, m, d] = parts(value);
  return `${monthNames[m - 1]} ${d}, ${y}`;
}

/** "Oct 13" */
export function formatDay(value: string): string {
  if (!isIsoDate(value)) return value;
  const [, m, d] = parts(value);
  return `${monthNames[m - 1]} ${d}`;
}

/** "2026-10" for grouping records by month. */
export function monthKey(value: string): string {
  return value.slice(0, 7);
}

/** "October 2026" from "2026-10". */
export function monthTitle(key: string): string {
  const [y, m] = key.split('-').map(Number);
  return m >= 1 && m <= 12 ? `${fullMonthNames[m - 1]} ${y}` : key;
}

/** A short, readable name for a range: "October 2026", "Aug 1 – Oct 31, 2026", "From Oct 1, 2026". */
export function describeRange(range: DateRange | null): string {
  if (!range) return 'Any time';
  const { from, to } = range;
  if (from && !to) return `From ${formatDate(from)}`;
  if (!from && to) return `Until ${formatDate(to)}`;

  const [fy, fm, fd] = parts(from);
  const [ty, tm, td] = parts(to);
  const wholeMonths = fd === 1 && td === daysIn(ty, tm);

  if (wholeMonths && fy === ty && fm === tm) return `${fullMonthNames[fm - 1]} ${fy}`;
  if (wholeMonths && fy === ty && fm === 1 && tm === 12) return String(fy);
  if (fy === ty) {
    if (fm === tm) return `${monthNames[fm - 1]} ${fd}–${td}, ${fy}`;
    return `${monthNames[fm - 1]} ${fd} – ${monthNames[tm - 1]} ${td}, ${fy}`;
  }
  return `${formatDate(from)} – ${formatDate(to)}`;
}
