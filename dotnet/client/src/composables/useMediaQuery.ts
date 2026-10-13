import { onBeforeUnmount, onMounted, ref } from 'vue';

// Tracks a CSS media query so script-driven behaviour (focus trapping, dialog semantics) can
// follow the same breakpoint the stylesheet uses.
export function useMediaQuery(query: string) {
  const matches = ref(false);
  let list: MediaQueryList | null = null;

  function update() {
    matches.value = list?.matches ?? false;
  }

  onMounted(() => {
    list = window.matchMedia(query);
    update();
    list.addEventListener('change', update);
  });

  onBeforeUnmount(() => list?.removeEventListener('change', update));

  return matches;
}
