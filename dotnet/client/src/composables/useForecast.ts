import { computed, reactive } from 'vue';
import { api, type ForecastData } from '../api';
import { canSeeForecast } from '../nav';
import { useAuth } from '../stores/auth';

// One forecast for the whole session. Home, the sidebar and the record pages all read it, so it
// is fetched once and reused for a few minutes instead of being recalculated on every page.
const FRESH_FOR_MS = 5 * 60_000;

const state = reactive({
  data: null as ForecastData | null,
  error: '',
  loading: false,
  loadedAt: 0,
  owner: '',
});

let pending: Promise<ForecastData | null> | null = null;

/** Booking changes can move the forecast, so the next read fetches it again. */
export function invalidateForecast() {
  state.loadedAt = 0;
}

export function useForecast() {
  const { state: auth } = useAuth();
  const allowed = computed(() => canSeeForecast(auth.user?.role));

  async function load(force = false): Promise<ForecastData | null> {
    if (!allowed.value) return null;

    // A different person signed in on this tab: nothing from the last session carries over.
    const who = auth.user?.email ?? '';
    if (state.owner !== who) {
      state.owner = who;
      state.data = null;
      state.loadedAt = 0;
    }

    const fresh = state.data !== null && Date.now() - state.loadedAt < FRESH_FOR_MS;
    if (!force && fresh) return state.data;
    if (pending) return pending;

    state.loading = true;
    pending = api
      .forecast()
      .then((data) => {
        state.data = data;
        state.error = '';
        state.loadedAt = Date.now();
        return data;
      })
      .catch((err: unknown) => {
        state.error = err instanceof Error ? err.message : 'The forecast could not be loaded.';
        return null;
      })
      .finally(() => {
        state.loading = false;
        pending = null;
      });
    return pending;
  }

  return { state, allowed, load };
}
