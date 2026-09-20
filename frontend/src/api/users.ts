import { apiRequest } from './client';
import type { CreateUserRequest, RoleOption, UpdateUserRequest, UserDetails, UserPage, UserQuery } from '../types/users';

export function getUsers(query: UserQuery, token: string) { const params = new URLSearchParams(); if (query.search) params.set('search', query.search); if (query.role) params.set('role', query.role); if (query.isActive !== undefined) params.set('isActive', String(query.isActive)); params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10)); return apiRequest<UserPage>(`/api/users?${params}`, {}, token); }
export const getUser = (id: number, token: string) => apiRequest<UserDetails>(`/api/users/${id}`, {}, token);
export const createUser = (request: CreateUserRequest, token: string) => apiRequest<UserDetails>('/api/users', { method: 'POST', body: JSON.stringify(request) }, token);
export const updateUser = (id: number, request: UpdateUserRequest, token: string) => apiRequest<UserDetails>(`/api/users/${id}`, { method: 'PUT', body: JSON.stringify(request) }, token);
export const getRoles = (token: string) => apiRequest<RoleOption[]>('/api/roles', {}, token);
