import { computed, ref } from 'vue';

// Chrome, Edge and Android browsers offer installation through this event. Safari has no such
// event, so the help topic explains Add to Home Screen there instead.
interface InstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

const offer = ref<InstallPromptEvent | null>(null);
const installed = ref(false);

/** Call once, before the app mounts: the browser may make its offer early in the page load. */
export function listenForInstall() {
  const standalone = window.matchMedia('(display-mode: standalone)').matches
    || (navigator as Navigator & { standalone?: boolean }).standalone === true;
  installed.value = standalone;

  window.addEventListener('beforeinstallprompt', (event) => {
    // Keep the offer for the account menu instead of the browser's own pop-up bar.
    event.preventDefault();
    offer.value = event as InstallPromptEvent;
  });
  window.addEventListener('appinstalled', () => {
    installed.value = true;
    offer.value = null;
  });
}

export function useInstall() {
  const canInstall = computed(() => offer.value !== null && !installed.value);

  async function install() {
    const event = offer.value;
    if (!event) return;
    // The browser allows one prompt per offer.
    offer.value = null;
    await event.prompt();
    if ((await event.userChoice).outcome === 'accepted') installed.value = true;
  }

  return { canInstall, install };
}
