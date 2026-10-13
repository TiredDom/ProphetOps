const LOCALE = 'en-PH';

export function peso(value: number): string {
  if (!Number.isFinite(value)) return '₱0';
  const rounded = Math.round(value);
  // A loss reads "−₱454,000", the sign before the currency, as accounts write it.
  return (rounded < 0 ? '−₱' : '₱') + Math.abs(rounded).toLocaleString(LOCALE);
}

export function pesoCompact(value: number): string {
  if (!Number.isFinite(value)) return '₱0';
  const abs = Math.abs(value);
  const sign = value < 0 ? '−₱' : '₱';
  if (abs >= 1_000_000) return sign + (abs / 1_000_000).toFixed(1).replace(/\.0$/, '') + 'M';
  if (abs >= 1_000) return sign + (abs / 1_000).toFixed(1).replace(/\.0$/, '') + 'k';
  return sign + Math.round(abs).toLocaleString(LOCALE);
}

export function count(value: number): string {
  if (!Number.isFinite(value)) return '0';
  return Math.round(value).toLocaleString(LOCALE);
}

export function digitsOnly(text: string): number {
  const digits = text.replace(/[^\d]/g, '');
  return digits ? Number(digits) : 0;
}
