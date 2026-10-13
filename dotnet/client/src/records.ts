/**
 * The next free code in a series such as BKG-2401: one past the highest number already used.
 * Counting the list instead would hand out a code that is taken whenever records were imported
 * with their own codes, and the save would fail on a duplicate.
 */
export function nextCode(prefix: string, codes: readonly string[], first: number): string {
  const pattern = new RegExp(`^${prefix}-(\\d+)$`, 'i');
  let highest = first - 1;
  for (const code of codes) {
    const match = pattern.exec(code.trim());
    if (match) highest = Math.max(highest, Number(match[1]));
  }
  return `${prefix}-${highest + 1}`;
}

function same(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  // A blank and a missing value read the same on a form.
  const blank = (v: unknown) => v === null || v === undefined || v === '';
  return blank(a) && blank(b);
}

export interface MergeResult<T> {
  merged: T;
  /** Fields someone else changed while the form was open. */
  changedByOthers: (keyof T)[];
  /** Fields both people changed, differently: the draft keeps its value and the field is flagged. */
  conflicts: (keyof T)[];
}

/**
 * Someone saved the record while it was open here. Rather than throw the draft away, take
 * their changes where the draft left a field alone, keep the draft where they did not touch
 * it, and flag the fields both changed so the person can choose.
 */
export function mergeDraft<T extends object>(base: T, mine: T, theirs: T, keys: readonly (keyof T)[]): MergeResult<T> {
  const merged = { ...mine };
  const changedByOthers: (keyof T)[] = [];
  const conflicts: (keyof T)[] = [];
  for (const key of keys) {
    if (same(theirs[key], base[key])) continue;
    changedByOthers.push(key);
    if (same(mine[key], base[key])) merged[key] = theirs[key];
    else if (!same(mine[key], theirs[key])) conflicts.push(key);
  }
  return { merged, changedByOthers, conflicts };
}

/** "Payment status", "Payment status and Notes", "Client, Price and Notes". */
export function listNames(names: readonly string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

/** Whether a date (yyyy-MM-dd) is after the business date. */
export function isAfter(date: string, today: string): boolean {
  return !!date && !!today && date > today;
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function looksLikeEmail(value: string): boolean {
  return EMAIL.test(value.trim());
}

/** Distinct, non-empty values in first-seen order, for a field's suggestions. */
export function suggestions(values: readonly (string | null | undefined)[], limit = 200): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const value of values) {
    const text = (value ?? '').trim();
    if (!text) continue;
    const key = text.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    out.push(text);
    if (out.length >= limit) break;
  }
  return out;
}
