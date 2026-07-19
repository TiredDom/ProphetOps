import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue';

// Reports the inner width of an element so a chart can size its viewBox to match what is
// actually on screen. The first measurement is taken synchronously rather than waiting on
// ResizeObserver, which only delivers on a rendered frame and so never arrives in headless
// or background tabs; the observer and the resize listener only track later changes.
// min is below the narrowest phone content width (~297px inside the shell at 375px) so the
// clamp never forces the viewBox wider than the box it is drawn into, which would squash it.
export function useContentWidth(target: Ref<HTMLElement | null>, fallback: number, min = 220) {
  const width = ref(fallback);
  let observer: ResizeObserver | null = null;

  function measure() {
    const element = target.value;
    if (!element) return;

    const style = getComputedStyle(element);
    const inner =
      element.clientWidth -
      parseFloat(style.paddingLeft || '0') -
      parseFloat(style.paddingRight || '0');

    if (inner > 0) width.value = Math.max(min, Math.round(inner));
  }

  function attach() {
    measure();
    if (!target.value || observer || typeof ResizeObserver === 'undefined') return;
    observer = new ResizeObserver(measure);
    observer.observe(target.value);
  }

  onMounted(() => window.addEventListener('resize', measure));

  onBeforeUnmount(() => {
    window.removeEventListener('resize', measure);
    observer?.disconnect();
    observer = null;
  });

  return { width, attach };
}
