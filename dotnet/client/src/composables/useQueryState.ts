import { onBeforeUnmount, reactive, watch } from 'vue';
import { onBeforeRouteLeave, useRoute, useRouter, type LocationQueryRaw } from 'vue-router';

type Validators<T> = Partial<Record<keyof T, (value: string) => boolean>>;

/**
 * Keeps a list page's filters and sort order in the address, so a filtered list survives a
 * reload and can be bookmarked or sent to someone. Values equal to their default stay out of
 * the address, and anything a validator rejects falls back to the default.
 */
export function useQueryState<T extends Record<string, string>>(defaults: T, validators: Validators<T> = {}): T {
  const route = useRoute();
  const router = useRouter();
  const path = route.path;
  const keys = Object.keys(defaults) as (keyof T & string)[];
  const state = reactive({ ...defaults }) as T;

  for (const key of keys) {
    const raw = route.query[key];
    const value = Array.isArray(raw) ? raw[0] : raw;
    if (typeof value !== 'string') continue;
    const accept = validators[key];
    if (!accept || accept(value)) (state as Record<string, string>)[key] = value;
  }

  // Typing in the search box changes the state on every keystroke. Browsers throttle history
  // updates, so the address catches up once typing pauses.
  let timer: ReturnType<typeof setTimeout> | undefined;

  function cancel() {
    if (timer) clearTimeout(timer);
    timer = undefined;
  }

  function write() {
    timer = undefined;
    const query: LocationQueryRaw = {};
    for (const key of keys) {
      const value = state[key];
      if (value && value !== defaults[key]) query[key] = value;
    }
    void router.replace({ path, query });
  }

  watch(
    () => keys.map((key) => state[key]).join('\u0000'),
    () => {
      cancel();
      timer = setTimeout(write, 250);
    },
  );

  // A late write would pull the visitor back to this page after they had moved on.
  onBeforeRouteLeave(cancel);
  onBeforeUnmount(cancel);

  return state;
}
