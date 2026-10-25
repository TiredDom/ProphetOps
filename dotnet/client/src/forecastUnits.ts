// How each forecast measure is written. Revenue is in pesos; bookings and passengers are counts,
// shown as whole numbers because a forecast of 4.4 bookings means "about 4".
import type { ForecastMeasure } from './api';
import { formatCompactPeso, formatConcisePeso } from './forecastChart';
import { count, peso } from './format';

export const measures: { key: ForecastMeasure; label: string }[] = [
  { key: 'revenue', label: 'Revenue' },
  { key: 'bookings', label: 'Bookings' },
  { key: 'passengers', label: 'Passengers' },
];

export function isMeasure(value: string): value is ForecastMeasure {
  return measures.some((m) => m.key === value);
}

export function measureOf(value: string | undefined | null): ForecastMeasure {
  return value && isMeasure(value) ? value : 'revenue';
}

/** "revenue", "bookings", "passengers": what the line on the chart is. */
export function measureNoun(measure: ForecastMeasure): string {
  return measure === 'revenue' ? 'revenue' : measure;
}

export function forecastTitle(measure: ForecastMeasure): string {
  if (measure === 'bookings') return 'Bookings forecast';
  if (measure === 'passengers') return 'Passenger forecast';
  return 'Revenue forecast';
}

/** Axis ticks: compact pesos, or plain whole numbers. */
export function formatAxis(value: number, measure: ForecastMeasure): string {
  return measure === 'revenue' ? formatCompactPeso(value) : count(value);
}

/** Short figures in sentences and evidence: ₱2.10M, or 8. */
export function formatShort(value: number, measure: ForecastMeasure): string {
  return measure === 'revenue' ? formatConcisePeso(value) : count(value);
}

/** Full figures in tables and tooltips: ₱2,096,918, or 8 bookings. */
export function formatFull(value: number, measure: ForecastMeasure): string {
  if (measure === 'revenue') return peso(value);
  return withUnit(value, measure);
}

/** "8 bookings", "1 passenger". */
export function withUnit(value: number, measure: ForecastMeasure): string {
  const rounded = Math.round(value);
  if (measure === 'revenue') return formatConcisePeso(value);
  const unit = measure === 'bookings' ? 'booking' : 'passenger';
  return `${count(rounded)} ${rounded === 1 ? unit : `${unit}s`}`;
}

/** Average misses keep one decimal for counts, since they are usually below ten. */
export function formatMiss(value: number, measure: ForecastMeasure): string {
  if (measure === 'revenue') return peso(value);
  const unit = measure === 'bookings' ? 'bookings' : 'passengers';
  return `${value.toFixed(1)} ${unit}`;
}

/** What the forecast measures, for the model details. */
export function measureDescription(measure: ForecastMeasure): string {
  if (measure === 'bookings') return 'Bookings per month, by booking date';
  if (measure === 'passengers') return 'Passengers per month, by booking date';
  return 'Booking revenue (₱)';
}
