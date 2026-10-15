import { onBeforeUnmount, onMounted, ref } from 'vue';

// Tracks a CSS media query so script-driven behaviour (focus trapping, dialog semantics, which
// layout a list renders) can follow the same breakpoint the stylesheet uses. It reads the query
// straight away, so the first render already uses the right layout.
export function useMediaQuery(query: string) {
  const list = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia(query) : null;
  const matches = ref(list?.matches ?? false);

  function update() {
    matches.value = list?.matches ?? false;
  }

  onMounted(() => {
    update();
    list?.addEventListener('change', update);
  });

  onBeforeUnmount(() => list?.removeEventListener('change', update));

  return matches;
}
