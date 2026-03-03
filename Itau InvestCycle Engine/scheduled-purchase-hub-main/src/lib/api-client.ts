import type { ApiError } from './types';

const TIMEOUT = 15000;

const SCHEDULED_BASE = import.meta.env.VITE_SCHEDULED_BASE_URL || 'http://localhost:5115';
const MARKETDATA_BASE = import.meta.env.VITE_MARKETDATA_BASE_URL || 'http://localhost:5136';

export class ApiClientError extends Error {
  public apiError?: ApiError;
  public status: number;

  constructor(message: string, status: number, apiError?: ApiError) {
    super(message);
    this.name = 'ApiClientError';
    this.status = status;
    this.apiError = apiError;
  }
}

async function request<T>(
  baseUrl: string,
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT);

  try {
    const res = await fetch(`${baseUrl}${path}`, {
      ...options,
      signal: controller.signal,
      headers: {
        ...(options.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
        ...options.headers,
      },
    });

    clearTimeout(timer);

    if (!res.ok) {
      let apiError: ApiError | undefined;
      try {
        const body = await res.json();
        if (body && body.erro && body.codigo) {
          apiError = body as ApiError;
        }
      } catch {
        // no JSON body
      }

      throw new ApiClientError(
        apiError?.erro || `Erro HTTP ${res.status}`,
        res.status,
        apiError
      );
    }

    // 204 or empty
    const text = await res.text();
    if (!text) return undefined as T;
    return JSON.parse(text) as T;
  } catch (err) {
    clearTimeout(timer);
    if (err instanceof ApiClientError) throw err;
    if (err instanceof DOMException && err.name === 'AbortError') {
      throw new ApiClientError('Timeout: serviço não respondeu', 0);
    }
    throw new ApiClientError(
      err instanceof Error ? err.message : 'Erro de conexão',
      0
    );
  }
}

// Scheduled Purchase Engine
export const scheduledApi = {
  get: <T>(path: string) => request<T>(SCHEDULED_BASE, path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(SCHEDULED_BASE, path, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(SCHEDULED_BASE, path, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    }),
  delete: <T>(path: string) =>
    request<T>(SCHEDULED_BASE, path, { method: 'DELETE' }),
};

// Market Data
export const marketDataApi = {
  get: <T>(path: string) => request<T>(MARKETDATA_BASE, path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(MARKETDATA_BASE, path, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  upload: <T>(path: string, formData: FormData) =>
    request<T>(MARKETDATA_BASE, path, {
      method: 'POST',
      body: formData,
    }),
};

// Ping helpers
export async function pingScheduled(): Promise<boolean> {
  try {
    await fetch(`${SCHEDULED_BASE}/api/admin/cesta/atual`, {
      method: 'GET',
      signal: AbortSignal.timeout(5000),
    });
    return true;
  } catch {
    return false;
  }
}

export async function pingMarketData(): Promise<boolean> {
  try {
    await fetch(`${MARKETDATA_BASE}/api/cotacoes?page=1&pageSize=1`, {
      method: 'GET',
      signal: AbortSignal.timeout(5000),
    });
    return true;
  } catch {
    return false;
  }
}
