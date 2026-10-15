import { computed, reactive } from 'vue';
import { api, describeError, type ForecastData, type ForecastEvaluation, type ForecastMeasure } from '../api';
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
  for (const key of Object.keys(tests) as ForecastMeasure[]) tests[key].loadedAt = 0;
}

interface TestEntry {
  data: ForecastEvaluation | null;
  error: string;
  loading: boolean;
  loadedAt: number;
  owner: string;
}

function testEntry(): TestEntry {
  return { data: null, error: '', loading: false, loadedAt: 0, owner: '' };
}

// The accuracy test reruns the forecast from every past month, so it is asked for only when someone
// opens the model details, and then kept like the forecast itself.
const tests = reactive<Record<ForecastMeasure, TestEntry>>({
  revenue: testEntry(),
  bookings: testEntry(),
  passengers: testEntry(),
});

const pendingTests: Partial<Record<ForecastMeasure, Promise<ForecastEvaluation | null>>> = {};

export function useForecastEvaluation(measure: () => ForecastMeasure) {
  const { state: auth } = useAuth();
  const allowed = computed(() => canSeeForecast(auth.user?.role));
  const state = computed(() => tests[measure()]);

  async function load(force = false): Promise<ForecastEvaluation | null> {
    if (!allowed.value) return null;
    const key = measure();
    const entry = tests[key];

    const who = auth.user?.email ?? '';
    if (entry.owner !== who) {
      entry.owner = who;
      entry.data = null;
      entry.loadedAt = 0;
    }

    const fresh = entry.data !== null && Date.now() - entry.loadedAt < FRESH_FOR_MS;
    if (!force && fresh) return entry.data;
    const inFlight = pendingTests[key];
    if (inFlight) return inFlight;

    entry.loading = true;
    entry.error = '';
    const request = api
      .forecastEvaluation(key)
      .then((data) => {
        entry.data = data;
        entry.loadedAt = Date.now();
        return data;
      })
      .catch((err: unknown) => {
        entry.error = describeError(err, 'The accuracy test could not be loaded. Try again in a moment.');
        return null;
      })
      .finally(() => {
        entry.loading = false;
        delete pendingTests[key];
      });
    pendingTests[key] = request;
    return request;
  }

  return { state, allowed, load };
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
        state.error = describeError(err, 'The forecast could not be loaded. Try again in a moment.');
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
