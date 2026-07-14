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

export class ApiError extends Error {
  status: number;
  fields: Record<string, string>;
  constructor(status: number, message: string, fields: Record<string, string> = {}) {
    super(message);
    this.status = status;
    this.fields = fields;
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: 'include',
    headers: body ? { 'Content-Type': 'application/json' } : {},
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
};
