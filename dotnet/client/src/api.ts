export interface AuthUser {
  name: string;
  email: string;
  role: string;
  defaultPath: string;
}

export interface DashboardForecast {
  method: string;
  horizon: number;
  accuracy: number;
  mape: number;
  nextValue: number;
  direction: 'up' | 'down' | 'flat';
  changePercent: number;
  peakMonth: string;
  peakValue: number;
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

export interface DashboardData {
  revenue: number;
  costs: number;
  estimatedProfit: number;
  bookings: number;
  packages: number;
  expenses: number;
  forecast: DashboardForecast;
  lowStockPackages: LowStockPackage[];
  pendingPayments: PendingPayments;
  recentBookings: RecentBooking[];
  lastUpdated: string;
}

export interface Booking {
  id: string;
  backendId: number;
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

export type BookingInput = Omit<Booking, 'backendId'> & { confirmUnusual?: boolean };

export interface PackageRow {
  id: string;
  backendId: number;
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

export interface AnalyticsPoint {
  label: string;
  value: number;
}

export interface AnalyticsData {
  salesHistory: AnalyticsPoint[];
  packageMix: AnalyticsPoint[];
  paymentBreakdown: AnalyticsPoint[];
  revenueByDestination: AnalyticsPoint[];
  totalRevenue: number;
  totalBookings: number;
  averageBooking: number;
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
  usingLiveRecords: boolean;
  liveMonthsAvailable: number;
  recordedMonths: number;
  minimumMonths: number;
  filledMonths: number;
}

export interface ForecastData {
  method: string;
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

export interface ReportBreakdown {
  label: string;
  value: number;
}

export interface ReportCounts {
  bookings: number;
  packages: number;
  expenses: number;
  users: number;
}

export interface ReportsData {
  revenue: number;
  costs: number;
  profit: number;
  counts: ReportCounts;
  bookingsByStatus: ReportBreakdown[];
  expensesByCategory: ReportBreakdown[];
  revenueByPackage: ReportBreakdown[];
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
  fields: Record<string, string>;
  constructor(status: number, message: string, fields: Record<string, string> = {}) {
    super(message);
    this.status = status;
    this.fields = fields;
  }
}

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
  return match ? decodeURIComponent(match[1]) : null;
}

async function unwrap<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = 'Request failed.';
    let fields: Record<string, string> = {};
    try {
      const data = await response.json();
      if (data && typeof data.message === 'string') message = data.message;
      else if (data && typeof data === 'object') fields = data as Record<string, string>;
    } catch {
      message = response.statusText;
    }
    throw new ApiError(response.status, message, fields);
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
  login: (email: string, password: string) =>
    request<AuthUser>('POST', '/api/auth/login', { email, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  me: () => request<AuthUser>('GET', '/api/auth/me'),
  dashboard: () => request<DashboardData>('GET', '/api/dashboard'),
  bookings: () => request<BookingsPayload>('GET', '/api/bookings'),
  createBooking: (input: BookingInput) => request<Booking>('POST', '/api/bookings', input),
  updateBooking: (code: string, input: BookingInput) =>
    request<Booking>('PUT', `/api/bookings/${code}`, input),
  voidBooking: (code: string, reason: string) =>
    request<Booking>('POST', `/api/bookings/${encodeURIComponent(code)}/void`, { reason }),
  restoreBooking: (code: string) =>
    request<Booking>('POST', `/api/bookings/${encodeURIComponent(code)}/restore`, {}),
  voidExpense: (code: string, reason: string) =>
    request<ExpenseRow>('POST', `/api/expenses/${encodeURIComponent(code)}/void`, { reason }),
  restoreExpense: (code: string) =>
    request<ExpenseRow>('POST', `/api/expenses/${encodeURIComponent(code)}/restore`, {}),
  activity: (entityType?: string, entityCode?: string) => {
    const params = new URLSearchParams();
    if (entityType) params.set('entityType', entityType);
    if (entityCode) params.set('entityCode', entityCode);
    const query = params.toString();
    return request<ActivityEntry[]>('GET', '/api/activity' + (query ? '?' + query : ''));
  },
  importBookingsPreview: (file: File) =>
    upload<ImportPreview>('/api/import/bookings/preview', file),
  importBookingsCommit: (file: File) =>
    upload<ImportResult>('/api/import/bookings/commit', file, { confirm: 'true' }),
  bulkBookings: (ids: string[], action: 'confirm' | 'paid') =>
    request<{ updated: number }>('POST', '/api/bookings/bulk', { ids, action }),
  packages: () => request<PackageRow[]>('GET', '/api/inventory'),
  createPackage: (input: PackageInput) => request<PackageRow>('POST', '/api/inventory', input),
  updatePackage: (code: string, input: PackageInput) =>
    request<PackageRow>('PUT', `/api/inventory/${code}`, input),
  uploadPackageImage: (code: string, file: File) =>
    upload<PackageRow>(`/api/inventory/${encodeURIComponent(code)}/image`, file),
  removePackageImage: (code: string) =>
    request<PackageRow>('DELETE', `/api/inventory/${encodeURIComponent(code)}/image`),
  expenses: () => request<ExpenseRow[]>('GET', '/api/expenses'),
  createExpense: (input: ExpenseInput) => request<ExpenseRow>('POST', '/api/expenses', input),
  updateExpense: (code: string, input: ExpenseInput) =>
    request<ExpenseRow>('PUT', `/api/expenses/${code}`, input),
  analytics: () => request<AnalyticsData>('GET', '/api/analytics'),
  forecast: () => request<ForecastData>('GET', '/api/forecast'),
  reports: () => request<ReportsData>('GET', '/api/reports'),
  users: () => request<UserRow[]>('GET', '/api/users'),
  userRoles: () => request<RoleOption[]>('GET', '/api/users/roles'),
  createUser: (input: UserInput) => request<UserRow>('POST', '/api/users', input),
  updateUser: (email: string, input: UserInput) =>
    request<UserRow>('PUT', `/api/users/${encodeURIComponent(email)}`, input),
};
