import { reactive } from 'vue';

const state = reactive({ open: false, topic: '' as string, paletteOpen: false });

/** Opens help beside the page, at a topic when one is named. */
export function openHelp(topic = '') {
  state.paletteOpen = false;
  state.topic = topic;
  state.open = true;
}

export function closeHelp() {
  state.open = false;
}

export function openPalette() {
  state.open = false;
  state.paletteOpen = true;
}

export function closePalette() {
  state.paletteOpen = false;
}

export function useHelpState() {
  return state;
}
