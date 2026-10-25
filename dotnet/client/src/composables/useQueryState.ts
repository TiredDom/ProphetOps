import { onBeforeUnmount, reactive, watch } from 'vue';
import { onBeforeRouteLeave, useRoute, useRouter, type LocationQuery, type LocationQueryRaw } from 'vue-router';

type Validators<T> = Partial<Record<keyof T, (value: string) => boolean>>;

/**
 * Keeps a list page's filters and sort order in the address, so a filtered list survives a
 * reload and can be bookmarked or sent to someone. Values equal to their default stay out of
 * the address, and anything a validator rejects falls back to the default. A link to the same
 * page with different filters (the sidebar, a band's button) updates the state as well.
 */
export function useQueryState<T extends Record<string, string>>(defaults: T, validators: Validators<T> = {}): T {
  const route = useRoute();
  const router = useRouter();
  const path = route.path;
  const keys = Object.keys(defaults) as (keyof T & string)[];
  const state = reactive({ ...defaults }) as T;

  function fromQuery(query: LocationQuery): T {
    const next = { ...defaults };
    for (const key of keys) {
      const raw = query[key];
      const value = Array.isArray(raw) ? raw[0] : raw;
      if (typeof value !== 'string') continue;
      const accept = validators[key];
      if (!accept || accept(value)) (next as Record<string, string>)[key] = value;
    }
    return next;
  }

  function toQuery(values: T): LocationQueryRaw {
    const query: LocationQueryRaw = {};
    for (const key of keys) {
      const value = values[key];
      if (value && value !== defaults[key]) query[key] = value;
    }
    return query;
  }

  const serialize = (query: LocationQueryRaw | LocationQuery) =>
    keys.map((key) => `${key}=${String(query[key] ?? '')}`).join('&');

  Object.assign(state, fromQuery(route.query));

  // Typing in the search box changes the state on every keystroke. Browsers throttle history
  // updates, so the address catches up once typing pauses.
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastWritten = serialize(toQuery(state));

  function cancel() {
    if (timer) clearTimeout(timer);
    timer = undefined;
  }

  function write() {
    timer = undefined;
    const query = toQuery(state);
    lastWritten = serialize(query);
    void router.replace({ path, query });
  }

  watch(
    () => keys.map((key) => state[key]).join('\u0000'),
    () => {
      cancel();
      timer = setTimeout(write, 250);
    },
  );

  // A change to the address that did not come from here, such as a link to this page with other
  // filters, becomes the new state. Our own writes are recognised and skipped, so a write that
  // lands while someone is still typing cannot undo their latest keystrokes.
  watch(
    () => route.query,
    (query) => {
      if (route.path !== path || serialize(query) === lastWritten) return;
      cancel();
      const next = fromQuery(query);
      lastWritten = serialize(toQuery(next));
      Object.assign(state, next);
    },
  );

  // A late write would pull the visitor back to this page after they had moved on.
  onBeforeRouteLeave(cancel);
  onBeforeUnmount(cancel);

  return state;
}
