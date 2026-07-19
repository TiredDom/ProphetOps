import { nextTick, onBeforeUnmount, onMounted, watch, type Ref } from 'vue';

const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type=hidden])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

// Keeps keyboard focus inside an open overlay and hands it back when the overlay closes.
// aria-modal only tells assistive technology that the rest of the page is inert; without
// this, Tab still walks out into the navigation behind the panel.
export function useModalFocus(container: Ref<HTMLElement | null>, isOpen: () => boolean) {
  let opener: HTMLElement | null = null;
  let locked = false;

  function reachable(): HTMLElement[] {
    const root = container.value;
    if (!root) return [];
    return [...root.querySelectorAll<HTMLElement>(FOCUSABLE)].filter((el) => {
      const rect = el.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    });
  }

  function onKeydown(event: KeyboardEvent) {
    if (event.key !== 'Tab' || !isOpen() || !container.value) return;

    const items = reachable();
    if (items.length === 0) {
      event.preventDefault();
      container.value.focus();
      return;
    }

    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement as HTMLElement | null;
    const inside = active ? container.value.contains(active) : false;

    if (!inside) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && (active === first || active === container.value)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  function lockScroll() {
    if (locked) return;
    document.body.style.overflow = 'hidden';
    locked = true;
  }

  function releaseScroll() {
    if (!locked) return;
    document.body.style.overflow = '';
    locked = false;
  }

  watch(isOpen, async (open) => {
    if (open) {
      opener = document.activeElement as HTMLElement | null;
      lockScroll();
      await nextTick();
      container.value?.focus();
    } else {
      releaseScroll();
      // Returning focus to whatever opened the panel keeps a keyboard user where they were,
      // rather than dropping them back at the top of the document.
      if (opener?.isConnected) opener.focus();
      opener = null;
    }
  });

  onMounted(() => document.addEventListener('keydown', onKeydown));

  onBeforeUnmount(() => {
    document.removeEventListener('keydown', onKeydown);
    releaseScroll();
  });
}
