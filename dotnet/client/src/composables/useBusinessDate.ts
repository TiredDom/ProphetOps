import { ref } from 'vue';
import { api } from '../api';

// The agency's business date (Manila), shared by the parts of the shell that need "this month"
// without each asking the server. It is read again after half an hour, so a tab left open over
// midnight moves on to the new day.
const KEEP_FOR_MS = 30 * 60_000;

const today = ref('');
let readAt = 0;
let pending: Promise<string> | null = null;

export function useBusinessDate() {
  async function load(): Promise<string> {
    if (today.value && Date.now() - readAt < KEEP_FOR_MS) return today.value;
    if (pending) return pending;
    pending = api
      .appConfig()
      .then((config) => {
        today.value = config.today;
        readAt = Date.now();
        return config.today;
      })
      .catch(() => today.value)
      .finally(() => {
        pending = null;
      });
    return pending;
  }

  return { today, load };
}
