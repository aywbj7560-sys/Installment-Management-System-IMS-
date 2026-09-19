import { apiRequest } from './client';
import type { GuarantorPage, GuarantorQuery } from '../types/guarantors';

export function getGuarantors(query: GuarantorQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.isActive !== undefined) params.set('isActive', String(query.isActive));
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<GuarantorPage>(`/api/guarantors?${params}`, {}, token);
}
