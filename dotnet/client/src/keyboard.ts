/** Whether a key press belongs to a field someone is typing in, where shortcuts stay out of the way. */
export function isTypingTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el || typeof el.tagName !== 'string') return false;
  if (el.isContentEditable) return true;
  const tag = el.tagName;
  if (tag === 'TEXTAREA' || tag === 'SELECT') return true;
  if (tag !== 'INPUT') return false;
  const type = (el as HTMLInputElement).type;
  return !['checkbox', 'radio', 'button', 'submit', 'reset', 'range', 'color', 'file'].includes(type);
}

export const isMac =
  typeof navigator !== 'undefined' && /Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent);

/** The modifier key as this computer labels it. */
export const modKey = isMac ? '⌘' : 'Ctrl';

/** Overlays on screen, leaving out any already on their way out. */
export function openOverlays(selector = '[aria-modal="true"], [role="menu"]'): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>(selector)].filter((el) => !el.closest('[class*="-leave-active"]'));
}

/** Whether an overlay (a panel, a dialog, a menu) is open, so page shortcuts wait. */
export function overlayOpen(): boolean {
  return openOverlays().length > 0;
}
