import { computed, reactive } from 'vue';
import { api, type ForecastData, type ForecastMeasure } from '../api';
import { canSeeForecast } from '../nav';
import { useAuth } from '../stores/auth';

// One forecast per measure for the whole session. Home, the sidebar and the record pages all read
// them, so each is fetched once and reused for a few minutes instead of being recalculated on
// every page.
const FRESH_FOR_MS = 5 * 60_000;

interface Entry {
  data: ForecastData | null;
  error: string;
  loading: boolean;
  loadedAt: number;
  owner: string;
}

function entry(): Entry {
  return { data: null, error: '', loading: false, loadedAt: 0, owner: '' };
}

const entries = reactive<Record<ForecastMeasure, Entry>>({
  revenue: entry(),
  bookings: entry(),
  passengers: entry(),
});

const pending: Partial<Record<ForecastMeasure, Promise<ForecastData | null>>> = {};

/** Booking changes can move every forecast, so the next read fetches them again. */
export function invalidateForecast() {
  for (const key of Object.keys(entries) as ForecastMeasure[]) entries[key].loadedAt = 0;
}

export function useForecast(measure: ForecastMeasure = 'revenue') {
  const { state: auth } = useAuth();
  const allowed = computed(() => canSeeForecast(auth.user?.role));
  const state = entries[measure];

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
    const inFlight = pending[measure];
    if (inFlight) return inFlight;

    state.loading = true;
    const request = api
      .forecast(measure)
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
        delete pending[measure];
      });
    pending[measure] = request;
    return request;
  }

  return { state, allowed, load };
}
