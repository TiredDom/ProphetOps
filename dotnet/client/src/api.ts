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

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
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
    try {
      const data = await response.json();
      if (data && typeof data.message === 'string') message = data.message;
    } catch {
      message = response.statusText;
    }
    throw new ApiError(response.status, message);
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
};
