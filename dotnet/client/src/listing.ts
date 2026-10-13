import { monthKey, monthTitle } from './periods';

export type SortDirection = 'asc' | 'desc';

/** Clicking the sorted column flips it; clicking another starts in that column's natural direction. */
export function nextSort(current: string, field: string, firstDirection: SortDirection): string {
  if (current.startsWith(`${field}-`)) return current.endsWith('-asc') ? `${field}-desc` : `${field}-asc`;
  return `${field}-${firstDirection}`;
}

export function sortParts(sort: string): { field: string; direction: SortDirection } {
  const cut = sort.lastIndexOf('-');
  const direction = sort.slice(cut + 1) === 'asc' ? 'asc' : 'desc';
  return { field: cut > 0 ? sort.slice(0, cut) : sort, direction };
}

export function compareText(a: string, b: string): number {
  return a.localeCompare(b, 'en', { sensitivity: 'base', numeric: true });
}

export interface ListSection<T, S> {
  key: string;
  /** Month heading, or null when the list is not in date order. */
  label: string | null;
  summary: S | null;
  items: T[];
}

/**
 * Splits a date-ordered list into months. Each month's summary covers every matching record
 * in that month, including any not shown yet, so a subtotal does not change as more of the
 * list is revealed.
 */
export function monthSections<T, S>(
  visible: readonly T[],
  all: readonly T[],
  dateOf: (item: T) => string,
  summarize: (items: T[]) => S,
): ListSection<T, S>[] {
  const byMonth = new Map<string, T[]>();
  for (const item of all) {
    const key = monthKey(dateOf(item));
    const bucket = byMonth.get(key);
    if (bucket) bucket.push(item);
    else byMonth.set(key, [item]);
  }

  const sections: ListSection<T, S>[] = [];
  for (const item of visible) {
    const key = monthKey(dateOf(item));
    const last = sections[sections.length - 1];
    if (last && last.key === key) {
      last.items.push(item);
    } else {
      sections.push({ key, label: monthTitle(key), summary: summarize(byMonth.get(key) ?? []), items: [item] });
    }
  }
  return sections;
}

export function singleSection<T, S>(visible: readonly T[]): ListSection<T, S>[] {
  return visible.length ? [{ key: 'all', label: null, summary: null, items: [...visible] }] : [];
}
