import { ref } from 'vue';

export type RecentKind = 'booking' | 'package' | 'expense' | 'account';

export interface RecentRecord {
  kind: RecentKind;
  code: string;
  label: string;
  detail: string;
}

const LIMIT = 8;
let owner = '';
const items = ref<RecentRecord[]>([]);

// Kept in this browser for the person signed in, so search can offer what they opened lately.
// Storage can be missing (a private window, blocked site data); then the list simply stays empty.
function key(): string {
  return `prophetops.recent.${owner}`;
}

function read(): RecentRecord[] {
  if (!owner) return [];
  try {
    const parsed = JSON.parse(localStorage.getItem(key()) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((item) => item && typeof item.code === 'string').slice(0, LIMIT) : [];
  } catch {
    return [];
  }
}

/** Points the list at the person signed in. */
export function useRecentsFor(email: string | undefined) {
  if ((email ?? '') !== owner) {
    owner = email ?? '';
    items.value = read();
  }
  return items;
}

/** A record someone opened goes to the top of their recent list. */
export function rememberRecent(item: RecentRecord) {
  if (!owner) return;
  const next = [item, ...items.value.filter((x) => !(x.kind === item.kind && x.code === item.code))].slice(0, LIMIT);
  items.value = next;
  try {
    localStorage.setItem(key(), JSON.stringify(next));
  } catch {
    // The list still works for this visit.
  }
}

export function recentPath(item: RecentRecord): string {
  const page = { booking: '/bookings', package: '/packages', expense: '/expenses', account: '/team' }[item.kind];
  return `${page}?open=${encodeURIComponent(item.code)}`;
}
