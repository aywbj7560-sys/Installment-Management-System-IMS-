import { apiRequest } from './client';
import type { Customer, CustomerPage, CustomerQuery, CustomerRequest } from '../types/customers';
export function getCustomers(query: CustomerQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.status) params.set('status', query.status);
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<CustomerPage>(`/api/customers?${params}`, {}, token);
}
export const getCustomer = (id: number, token: string) => apiRequest<Customer>(`/api/customers/${id}`, {}, token);
export const createCustomer = (request: CustomerRequest, token: string) => apiRequest<Customer>('/api/customers', { method: 'POST', body: JSON.stringify(request) }, token);
export const updateCustomer = (id: number, request: CustomerRequest, token: string) => apiRequest<Customer>(`/api/customers/${id}`, { method: 'PUT', body: JSON.stringify(request) }, token);
