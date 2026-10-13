import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuth } from '../stores/auth';

const KEEP_MS = 24 * 60 * 60 * 1000;

interface Stored<T> {
  at: number;
  value: T;
}

const timeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' });
const dayFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });

interface DraftOptions<T> {
  /** True while a new record's form is open (edits are not kept). */
  active: () => boolean;
  /** True when the form holds something worth keeping. */
  dirty: () => boolean;
  snapshot: () => T;
}

/**
 * A new record's unfinished form, kept in this browser for the person typing it, so a reload, a
 * lost connection or a phone closing the tab does not lose it. It is offered back the next time
 * the form opens, and goes once the record is saved or the form is closed on purpose. Storage can
 * be missing (a private window, blocked site data); then nothing is kept and the form still works.
 */
export function useFormDraft<T extends object>(kind: string, options: DraftOptions<T>) {
  const { state: auth } = useAuth();
  /** When the draft now in the form was started, if one was restored; 0 otherwise. */
  const restoredAt = ref(0);
  let started = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;

  function key(): string {
    return `prophetops.draft.${kind}.${auth.user?.email ?? ''}`;
  }

  function forget() {
    try {
      localStorage.removeItem(key());
    } catch {
      // Nothing was kept.
    }
  }

  function flush() {
    clearTimeout(timer);
    if (!options.active()) return;
    if (!options.dirty()) {
      forget();
      return;
    }
    if (!started) started = Date.now();
    try {
      localStorage.setItem(key(), JSON.stringify({ at: started, value: options.snapshot() } satisfies Stored<T>));
    } catch {
      // The form still works; it just is not kept.
    }
  }

  watch(
    () => (options.active() ? JSON.stringify(options.snapshot()) : ''),
    (now) => {
      if (!now) return;
      clearTimeout(timer);
      timer = setTimeout(flush, 400);
    },
  );

  /** The kept draft, if there is one; the form shows it as restored. */
  function restore(): T | null {
    try {
      const raw = localStorage.getItem(key());
      const stored = raw ? (JSON.parse(raw) as Stored<T>) : null;
      if (stored && typeof stored.at === 'number' && typeof stored.value === 'object' && Date.now() - stored.at <= KEEP_MS) {
        started = stored.at;
        restoredAt.value = stored.at;
        return stored.value;
      }
      if (stored) forget();
    } catch {
      // Unreadable: start clean.
    }
    started = 0;
    restoredAt.value = 0;
    return null;
  }

  /** The record was saved, or the form was closed on purpose: the draft goes. */
  function discard() {
    clearTimeout(timer);
    forget();
    started = 0;
    restoredAt.value = 0;
  }

  function startedLabel(): string {
    if (!restoredAt.value) return '';
    const at = new Date(restoredAt.value);
    return at.toDateString() === new Date().toDateString() ? `at ${timeFormat.format(at)}` : `on ${dayFormat.format(at)}`;
  }

  // Leaving the page (a reload, a phone closing the tab) keeps the latest keystrokes too.
  onMounted(() => window.addEventListener('pagehide', flush));
  onBeforeUnmount(() => {
    window.removeEventListener('pagehide', flush);
    clearTimeout(timer);
  });

  return { restoredAt, restore, discard, startedLabel };
}
