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
}

export interface DashboardData {
  revenue: number;
  costs: number;
  estimatedProfit: number;
  bookings: number;
  packages: number;
  expenses: number;
  forecast: DashboardForecast;
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

export type BookingInput = Omit<Booking, 'backendId'>;

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
  value: number;
  lower: number;
  upper: number;
}

export interface ForecastInsight {
  direction: 'up' | 'down' | 'flat';
  changePercent: number;
  peakMonth: string;
  peakValue: number;
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

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  if (body) headers['Content-Type'] = 'application/json';
  if (method !== 'GET' && method !== 'HEAD') {
    const token = readCookie('XSRF-TOKEN');
    if (token) headers['X-XSRF-TOKEN'] = token;
  }

  const response = await fetch(url, {
    method,
    credentials: 'include',
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

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

export const api = {
  login: (email: string, password: string) =>
    request<AuthUser>('POST', '/api/auth/login', { email, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  me: () => request<AuthUser>('GET', '/api/auth/me'),
  dashboard: () => request<DashboardData>('GET', '/api/dashboard'),
  bookings: () => request<BookingsPayload>('GET', '/api/bookings'),
  createBooking: (input: BookingInput) => request<Booking>('POST', '/api/bookings', input),
  bulkBookings: (ids: string[], action: 'confirm' | 'paid') =>
    request<{ updated: number }>('POST', '/api/bookings/bulk', { ids, action }),
  packages: () => request<PackageRow[]>('GET', '/api/inventory'),
  createPackage: (input: PackageInput) => request<PackageRow>('POST', '/api/inventory', input),
  updatePackage: (code: string, input: PackageInput) =>
    request<PackageRow>('PUT', `/api/inventory/${code}`, input),
  expenses: () => request<ExpenseRow[]>('GET', '/api/expenses'),
  createExpense: (input: ExpenseInput) => request<ExpenseRow>('POST', '/api/expenses', input),
  updateExpense: (code: string, input: ExpenseInput) =>
    request<ExpenseRow>('PUT', `/api/expenses/${code}`, input),
  analytics: () => request<AnalyticsData>('GET', '/api/analytics'),
  forecast: () => request<ForecastData>('GET', '/api/forecast'),
  reports: () => request<ReportsData>('GET', '/api/reports'),
  users: () => request<UserRow[]>('GET', '/api/users'),
};
