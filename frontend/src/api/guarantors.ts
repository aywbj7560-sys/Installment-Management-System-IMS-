import { apiRequest } from './client';
import type { Guarantor, GuarantorDetails, GuarantorPage, GuarantorQuery, GuarantorRequest } from '../types/guarantors';

export function getGuarantors(query: GuarantorQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.isActive !== undefined) params.set('isActive', String(query.isActive));
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<GuarantorPage>(`/api/guarantors?${params}`, {}, token);
}
export const getGuarantor = (id: number, token: string) => apiRequest<GuarantorDetails>(`/api/guarantors/${id}`, {}, token);
export const createGuarantor = (request: GuarantorRequest, token: string) => apiRequest<Guarantor>('/api/guarantors', { method: 'POST', body: JSON.stringify(request) }, token);
export const updateGuarantor = (id: number, request: GuarantorRequest, token: string) => apiRequest<Guarantor>(`/api/guarantors/${id}`, { method: 'PUT', body: JSON.stringify(request) }, token);
