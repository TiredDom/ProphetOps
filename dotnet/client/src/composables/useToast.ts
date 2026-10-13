import { reactive } from 'vue';

export type ToastTone = 'success' | 'error' | 'info';

export interface ToastAction {
  label: string;
  /** Runs once; the notice says it is working, then closes. */
  run: () => Promise<void> | void;
  /** An undo, which Ctrl+Z (⌘Z) also reaches while the notice is up. */
  undo?: boolean;
}

export interface Toast {
  id: number;
  tone: ToastTone;
  message: string;
  action?: ToastAction;
  busy: boolean;
}

export interface ToastOptions {
  action?: ToastAction;
}

const toasts = reactive<Toast[]>([]);
const timers = new Map<number, ReturnType<typeof setTimeout>>();
let seq = 0;

// Confirmations close on their own. One that can be undone stays long enough to reach its
// button, and any notice stays put while the pointer or focus is on it.
const PLAIN_MS = 5000;
const ACTION_MS = 10000;
const AFTER_LEAVE_MS = 4000;

function clearTimer(id: number) {
  const timer = timers.get(id);
  if (timer) clearTimeout(timer);
  timers.delete(id);
}

function dismiss(id: number) {
  clearTimer(id);
  const index = toasts.findIndex((t) => t.id === id);
  if (index !== -1) toasts.splice(index, 1);
}

function schedule(id: number, ms: number) {
  clearTimer(id);
  timers.set(id, setTimeout(() => dismiss(id), ms));
}

function push(tone: ToastTone, message: string, options: ToastOptions = {}): number {
  const id = (seq += 1);
  toasts.push({ id, tone, message, action: options.action, busy: false });

  // Errors stay until dismissed: something going wrong is not a message to snatch away from
  // someone still reading it.
  if (tone !== 'error') schedule(id, options.action ? ACTION_MS : PLAIN_MS);
  return id;
}

function hold(id: number) {
  clearTimer(id);
}

function release(id: number) {
  const toast = toasts.find((t) => t.id === id);
  if (toast && toast.tone !== 'error' && !toast.busy) schedule(id, AFTER_LEAVE_MS);
}

async function runAction(id: number) {
  const toast = toasts.find((t) => t.id === id);
  if (!toast?.action || toast.busy) return;
  toast.busy = true;
  clearTimer(id);
  try {
    await toast.action.run();
  } finally {
    dismiss(id);
  }
}

/** Ctrl+Z (⌘Z): undoes the latest change whose notice is still on screen. */
function undoLatest(): boolean {
  for (let i = toasts.length - 1; i >= 0; i -= 1) {
    const toast = toasts[i];
    if (toast.action?.undo && !toast.busy) {
      void runAction(toast.id);
      return true;
    }
  }
  return false;
}

/** An Undo button for a confirmation. */
export function undoAction(run: () => Promise<void> | void): ToastAction {
  return { label: 'Undo', run, undo: true };
}

export function useToast() {
  return {
    success: (message: string, options?: ToastOptions) => push('success', message, options),
    error: (message: string) => push('error', message),
    info: (message: string, options?: ToastOptions) => push('info', message, options),
  };
}

export function useToastList() {
  return { toasts, dismiss, hold, release, runAction, undoLatest };
}
