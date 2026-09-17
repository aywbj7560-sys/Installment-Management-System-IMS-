import { apiRequest } from './client';
import type { CurrentUser, LoginRequest, LoginResponse } from '../types/api';
export const loginRequest = (body: LoginRequest) => apiRequest<LoginResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify(body) });
export const currentUserRequest = (token: string) => apiRequest<CurrentUser>('/api/auth/me', {}, token);
