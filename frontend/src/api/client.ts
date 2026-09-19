const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8081').replace(/\/$/, '');
let unauthorizedHandler: (() => void) | undefined;
export const setUnauthorizedHandler = (handler?: () => void) => { unauthorizedHandler = handler; };
export class ApiError extends Error {
  constructor(public status: number, message: string, public errors: Record<string, string[]> = {}) { super(message); }
}
export async function apiRequest<T>(path: string, init: RequestInit = {}, token?: string | null): Promise<T> {
  try {
    const response = await fetch(`${baseUrl}${path}`, { ...init, headers: { 'Content-Type': 'application/json', ...init.headers, ...(token ? { Authorization: `Bearer ${token}` } : {}) } });
    if (!response.ok) {
      if (response.status === 401) unauthorizedHandler?.();
      const body = await response.json().catch(() => ({})) as { message?: string; detail?: string; errors?: Record<string, string[]> };
      throw new ApiError(response.status, response.status === 403 ? 'You do not have permission to perform this action.' : body.message || body.detail || 'The request could not be completed.', body.errors ?? {});
    }
    return response.json() as Promise<T>;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError(0, 'Unable to reach the server. Please try again.');
  }
}
