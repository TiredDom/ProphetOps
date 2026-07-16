import { reactive } from 'vue';

export type ToastTone = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  tone: ToastTone;
  message: string;
}

const toasts = reactive<Toast[]>([]);
let seq = 0;

function dismiss(id: number) {
  const index = toasts.findIndex((t) => t.id === id);
  if (index !== -1) toasts.splice(index, 1);
}

function push(tone: ToastTone, message: string) {
  const id = (seq += 1);
  toasts.push({ id, tone, message });
  window.setTimeout(() => dismiss(id), 4000);
}

export function useToast() {
  return {
    success: (message: string) => push('success', message),
    error: (message: string) => push('error', message),
    info: (message: string) => push('info', message),
  };
}

export function useToastList() {
  return { toasts, dismiss };
}
