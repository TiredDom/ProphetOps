export interface AuthUser {
  name: string;
  email: string;
  role: string;
  defaultPath: string;
}

export interface AppConfig {
  today: string;
  timeZone: string;
}

export interface DashboardForecast {
  method: string;
  horizon: number;
  ok: boolean;
  status: 'live' | 'sample' | 'insufficient-history';
  accuracy: number;
  mape: number;
  nextValue: number;
  direction: 'up' | 'down' | 'flat';
  changePercent: number;
  peakMonth: string;
  peakValue: number;
  dataSource: ForecastDataSource;
}

export interface LowStockPackage {
  code: string;
  packageName: string;
  destination: string;
  availableSlots: number;
  status: string;
}

export interface PendingPayments {
  count: number;
  amount: number;
}

export interface RecentBooking {
  code: string;
  client: string;
  package: string;
  destination: string;
  grossRevenue: number;
  paymentStatus: string;
  bookingStatus: string;
  ds: string;
}

/** Totals for an inclusive range of business dates (yyyy-MM-dd), voided records excluded. */
export interface PeriodTotals {
  from: string;
  to: string;
  revenue: number;
  bookings: number;
  expenses: number;
}

export interface DashboardData {
  totalsScope: 'lifetime';
  excludesVoided: true;
  revenue: number;
  costs: number;
  estimatedProfit: number;
  bookings: number;
  packages: number;
  expenses: number;
  /** This month so far, and the same run of days in the previous month. */
  thisMonth?: { current: PeriodTotals; previous: PeriodTotals };
  forecast: DashboardForecast;
  lowStockPackages: LowStockPackage[];
  pendingPayments: PendingPayments;
  recentBookings: RecentBooking[];
  lastUpdated: string;
}

export interface Booking {
  id: string;
  backendId: number;
  revision: number;
  ds: string;
  y: number;
  client: string;
  package: string;
  packageId: string | null;
  entryType: string;
  destination: string;
  grossRevenue: number;
  paymentStatus: string;
  bookingStatus: string;
  staffAssigned: string | null;
  source: string;
  notes: string | null;
  voided?: boolean;
  voidReason?: string | null;
}

export interface ImportNote {
  line: number;
  reason: string;
}

export interface ImportPreview {
  valid: number;
  skipped: number;
  duplicates: number;
  duplicateCodes: string[];
  from: string | null;
  to: string | null;
  months: number;
  passengers: number;
  totalRevenue: number;
  problems: ImportNote[];
  warnings: ImportNote[];
}

export interface ImportResult {
  batch: string;
  imported: number;
  skipped: number;
  duplicates: number;
  duplicateCodes: string[];
  from: string | null;
  to: string | null;
  totalRevenue: number;
  problems: ImportNote[];
  warnings: ImportNote[];
}

export interface PackageImportPreview {
  valid: number;
  skipped: number;
  duplicates: number;
  duplicateNames: string[];
  destinations: number;
  totalSlots: number;
  problems: ImportNote[];
  warnings: ImportNote[];
}

export interface PackageImportResult {
  batch: string;
  imported: number;
  skipped: number;
  duplicates: number;
  duplicateNames: string[];
  totalSlots: number;
  problems: ImportNote[];
  warnings: ImportNote[];
}

export interface ActivityEntry {
  at: string;
  actor: string;
  actorName: string;
  action: string;
  entityType: string;
  entityCode: string;
  summary: string | null;
}

export interface PackageOption {
  id: string;
  backendId: number;
  revision: number;
  packageName: string;
  destination: string;
  basePrice: number;
  availableSlots: number;
  duration: string | null;
}

export interface BookingsPayload {
  bookings: Booking[];
  packages: PackageOption[];
}

export type BookingInput = Omit<Booking, 'backendId' | 'revision' | 'voided' | 'voidReason'> & {
  revision?: number | null;
  confirmUnusual?: boolean;
};

export interface PackageRow {
  id: string;
  backendId: number;
  revision: number;
  packageName: string;
  destination: string;
  duration: string | null;
  basePrice: number;
  inclusions: string | null;
  availableSlots: number;
  soldCount: number;
  reservedCount: number;
  status: string;
  imageUrl: string | null;
}

export interface PackageInput {
  id: string;
  revision?: number | null;
  packageName: string;
  destination: string;
  duration: string | null;
  basePrice: number;
  inclusions: string | null;
  availableSlots: number;
  soldCount: number;
  reservedCount: number;
  status: string;
}

export interface ExpenseRow {
  id: string;
  backendId: number;
  date: string;
  category: string;
  amount: number;
  relatedPackage: string;
  paymentStatus: string;
  notes: string | null;
  voided?: boolean;
  voidReason?: string | null;
}

export interface ExpenseInput {
  id: string;
  date: string;
  category: string;
  amount: number;
  relatedPackage: string;
  paymentStatus: string;
  notes: string | null;
}

export interface ForecastParams {
  alpha: number;
  beta: number;
  gamma: number;
}

export interface ForecastMetricsView {
  mae: number;
  rmse: number;
  mape: number;
  sampleSize: number;
}

export interface ForecastBaselines {
  seasonalNaiveMae: number;
  naiveMae: number;
}

export interface ForecastPoint {
  label: string;
  month: string;
  value: number;
}

export interface ForecastStepView {
  step: number;
  month: string;
  monthLabel: string;
  value: number;
  lower: number;
  upper: number;
}

export interface TrajectoryNote {
  kind: string;
  text: string;
}

export interface ForecastInsight {
  direction: 'up' | 'down' | 'flat';
  changePercent: number;
  peakMonth: string;
  peakValue: number;
  notes: TrajectoryNote[];
}

export interface ForecastDataSource {
  status: 'live' | 'sample' | 'insufficient-history';
  usingLiveRecords: boolean;
  usingSample: boolean;
  label: string;
  liveMonthsAvailable: number;
  recordedMonths: number;
  minimumMonths: number;
  filledMonths: number;
  lastRecordedMonth: string | null;
}

/** What a forecast adds up each month. Counts are by booking date, not travel date. */
export type ForecastMeasure = 'revenue' | 'bookings' | 'passengers';

export interface ForecastData {
  method: string;
  /** Present on every forecast since the measure was added; revenue when absent. */
  measure?: ForecastMeasure;
  unit?: string;
  seasonLength: number;
  horizon: number;
  ok: boolean;
  accuracy: number;
  params: ForecastParams;
  metrics: ForecastMetricsView;
  baselines: ForecastBaselines;
  insight: ForecastInsight;
  dataSource: ForecastDataSource;
  history: ForecastPoint[];
  steps: ForecastStepView[];
}

/** How the forecasts made a given number of months ahead did in the accuracy test. */
export interface ForecastEvaluationHorizon {
  step: number;
  forecasts: number;
  mae: number;
  /** Percentages leave out months with nothing recorded; null when every month had nothing. */
  mape: number | null;
  seasonalNaiveMae: number;
  seasonalNaiveMape: number | null;
  insideRange: number;
  coverage: number;
}

export interface ForecastEvaluationOverall extends Omit<ForecastEvaluationHorizon, 'step'> {
  /** How much smaller the Holt-Winters miss was than the baseline's; below zero when it was larger. */
  skillVsSeasonalNaive: number | null;
}

export interface ForecastEvaluationPoint {
  madeAfter: string;
  month: string;
  step: number;
  actual: number;
  forecast: number;
  lower: number;
  upper: number;
  sameMonthLastYear: number;
  insideRange: boolean;
}

/**
 * The forecast tested on recorded months it had not seen: rerun from each past month using only
 * the months before it, and compared with what was recorded and with the same month a year earlier.
 */
export interface ForecastEvaluation {
  method: string;
  measure: ForecastMeasure;
  unit: string;
  ok: boolean;
  reason: string | null;
  seasonLength: number;
  horizon: number;
  monthsAvailable: number;
  monthsRequired: number;
  origins: number;
  firstTestedMonth?: string;
  lastTestedMonth?: string;
  dataSource: ForecastDataSource;
  byHorizon: ForecastEvaluationHorizon[];
  overall: ForecastEvaluationOverall | null;
  points: ForecastEvaluationPoint[];
}

export interface ReportLine {
  label: string;
  /** Pesos for revenue and spending lines; a count for booking stages and payment states. */
  value: number;
  count?: number;
  /** Booked value, on payment lines. */
  revenue?: number;
}

export interface ReportMonth {
  month: string;
  label: string;
  revenue: number;
  costs: number;
  afterCosts: number;
  bookings: number;
  passengers: number;
}

export interface ReportComparison {
  from: string | null;
  to: string | null;
  revenue: number;
  costs: number;
  profit: number;
  bookings: number;
  passengers: number;
}

export interface ReportCounts {
  bookings: number;
  passengers: number;
  packages: number;
  expenses: number;
  users: number;
}

export interface ReportsData {
  totalsScope: 'lifetime' | 'period';
  excludesVoided: true;
  /** `through` is where the figures stop: today, when the period runs past it. */
  period: { from: string | null; to: string | null; through: string | null };
  revenue: number;
  costs: number;
  /** Gross booking revenue less recorded expenses, not accounting profit. */
  profit: number;
  counts: ReportCounts;
  previous: ReportComparison | null;
  monthly: ReportMonth[];
  bookingsByStatus: ReportLine[];
  bookingsByPayment: ReportLine[];
  expensesByCategory: ReportLine[];
  revenueByPackage: ReportLine[];
  revenueByDestination: ReportLine[];
  generatedAt: string;
}

export interface UserRow {
  name: string;
  email: string;
  role: string;
  status: string;
  lastLoginAt: string | null;
}

export interface UserInput {
  name: string;
  email: string;
  role: string;
  password: string;
  status: string;
}

export interface RoleOption {
  name: string;
  access: string;
}

export class ApiError extends Error {
  status: number;
  code?: string;
  fields: Record<string, string>;
  constructor(status: number, message: string, fields: Record<string, string> = {}, code?: string) {
    super(message);
    this.status = status;
    this.fields = fields;
    this.code = code;
  }
}

/** Words for any failure: the server's own, or a plain line when the server could not be reached. */
export function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof TypeError) return 'Could not reach ProphetOps. Check the connection, then try again.';
  return fallback;
}

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
  return match ? decodeURIComponent(match[1]) : null;
}

/**
 * What to say when the server gives no words of its own: plain language, what happened to the
 * person's work, and what to do next. Never a status code or "Unauthorized".
 */
function plainMessage(status: number): string {
  if (status === 401) return 'You have been signed out. Sign in again in another tab, then try again here; nothing on this page is lost.';
  if (status === 403) return 'Your account cannot do this. Ask an owner if you need access.';
  if (status === 404) return 'This record could not be found. It may have been removed; refresh the page to see the latest.';
  if (status === 413) return 'That file is too large to upload.';
  if (status === 429) return 'Too many tries in a short time. Wait a minute, then try again.';
  if (status >= 500) return 'The server ran into a problem, so nothing was saved. Try again in a moment.';
  return 'That could not be done. Check the details, then try again.';
}

async function unwrap<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = '';
    let fields: Record<string, string> = {};
    let code: string | undefined;
    try {
      const data = await response.json();
      if (data && typeof data === 'object') {
        const record = data as Record<string, unknown>;
        if (typeof record.code === 'string') code = record.code;
        if (typeof record.message === 'string') message = record.message;
        fields = Object.fromEntries(
          Object.entries(record).filter(
            ([key, value]) => key !== 'code' && key !== 'message' && typeof value === 'string',
          ),
        ) as Record<string, string>;
      }
    } catch {
      // No body, or not JSON: the plain message below stands in.
    }
    throw new ApiError(response.status, message || plainMessage(response.status), fields, code);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function csrfHeaders(): Record<string, string> {
  const token = readCookie('XSRF-TOKEN');
  return token ? { 'X-XSRF-TOKEN': token } : {};
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  if (body) headers['Content-Type'] = 'application/json';
  if (method !== 'GET' && method !== 'HEAD') Object.assign(headers, csrfHeaders());

  return unwrap<T>(
    await fetch(url, {
      method,
      credentials: 'include',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    }),
  );
}

async function upload<T>(url: string, file: File, fields?: Record<string, string>): Promise<T> {
  const body = new FormData();
  body.append('file', file);
  if (fields) for (const [key, value] of Object.entries(fields)) body.append(key, value);

  // No Content-Type header here on purpose — the browser has to set it so the multipart
  // boundary matches the body it generated.
  return unwrap<T>(
    await fetch(url, { method: 'POST', credentials: 'include', headers: csrfHeaders(), body }),
  );
}

export const api = {
  appConfig: () => request<AppConfig>('GET', '/api/app/config'),
  login: (email: string, password: string) =>
    request<AuthUser>('POST', '/api/auth/login', { email, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  me: () => request<AuthUser>('GET', '/api/auth/me'),
  dashboard: () => request<DashboardData>('GET', '/api/dashboard'),
  bookings: () => request<BookingsPayload>('GET', '/api/bookings'),
  createBooking: (input: BookingInput) => request<Booking>('POST', '/api/bookings', input),
  updateBooking: (code: string, input: BookingInput) =>
    request<Booking>('PUT', `/api/bookings/${code}`, input),
  voidBooking: (code: string, reason: string, revision: number) =>
    request<Booking>('POST', `/api/bookings/${encodeURIComponent(code)}/void`, { reason, revision }),
  restoreBooking: (code: string, revision: number) =>
    request<Booking>('POST', `/api/bookings/${encodeURIComponent(code)}/restore`, { revision }),
  voidExpense: (code: string, reason: string) =>
    request<ExpenseRow>('POST', `/api/expenses/${encodeURIComponent(code)}/void`, { reason }),
  restoreExpense: (code: string) =>
    request<ExpenseRow>('POST', `/api/expenses/${encodeURIComponent(code)}/restore`, {}),
  activity: (entityType?: string, entityCode?: string, take?: number) => {
    const params = new URLSearchParams();
    if (entityType) params.set('entityType', entityType);
    if (entityCode) params.set('entityCode', entityCode);
    if (take) params.set('take', String(take));
    const query = params.toString();
    return request<ActivityEntry[]>('GET', '/api/activity' + (query ? '?' + query : ''));
  },
  importBookingsPreview: (file: File) =>
    upload<ImportPreview>('/api/import/bookings/preview', file),
  importBookingsCommit: (file: File) =>
    upload<ImportResult>('/api/import/bookings/commit', file, { confirm: 'true' }),
  importPackagesPreview: (file: File) =>
    upload<PackageImportPreview>('/api/import/packages/preview', file),
  importPackagesCommit: (file: File) =>
    upload<PackageImportResult>('/api/import/packages/commit', file, { confirm: 'true' }),
  bulkBookings: (ids: string[], action: 'confirm' | 'paid', revisions: Record<string, number>) =>
    request<{ updated: number; bookings: Booking[] }>('POST', '/api/bookings/bulk', { ids, action, revisions }),
  packages: () => request<PackageRow[]>('GET', '/api/inventory'),
  createPackage: (input: PackageInput) => request<PackageRow>('POST', '/api/inventory', input),
  updatePackage: (code: string, input: PackageInput) =>
    request<PackageRow>('PUT', `/api/inventory/${code}`, input),
  uploadPackageImage: (code: string, file: File, revision: number) =>
    upload<PackageRow>(`/api/inventory/${encodeURIComponent(code)}/image`, file, { revision: String(revision) }),
  removePackageImage: (code: string, revision: number) =>
    request<PackageRow>('DELETE', `/api/inventory/${encodeURIComponent(code)}/image`, { revision }),
  expenses: () => request<ExpenseRow[]>('GET', '/api/expenses'),
  createExpense: (input: ExpenseInput) => request<ExpenseRow>('POST', '/api/expenses', input),
  updateExpense: (code: string, input: ExpenseInput) =>
    request<ExpenseRow>('PUT', `/api/expenses/${code}`, input),
  forecast: (measure: ForecastMeasure = 'revenue') =>
    request<ForecastData>('GET', measure === 'revenue' ? '/api/forecast' : `/api/forecast?measure=${measure}`),
  forecastEvaluation: (measure: ForecastMeasure = 'revenue') =>
    request<ForecastEvaluation>('GET', `/api/forecast/evaluation?measure=${measure}`),
  reports: (from = '', to = '') => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const query = params.toString();
    return request<ReportsData>('GET', '/api/reports' + (query ? '?' + query : ''));
  },
  users: () => request<UserRow[]>('GET', '/api/users'),
  userRoles: () => request<RoleOption[]>('GET', '/api/users/roles'),
  createUser: (input: UserInput) => request<UserRow>('POST', '/api/users', input),
  updateUser: (email: string, input: UserInput) =>
    request<UserRow>('PUT', `/api/users/${encodeURIComponent(email)}`, input),
};
