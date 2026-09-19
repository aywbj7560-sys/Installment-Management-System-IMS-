import { apiRequest } from './client';
import type { ActivateContractRequest, ContractDetails, ContractPage, ContractQuery, ContractRequest } from '../types/contracts';

export function getContracts(query: ContractQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.customerId !== undefined) params.set('customerId', String(query.customerId));
  if (query.status) params.set('status', query.status);
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<ContractPage>(`/api/contracts?${params}`, {}, token);
}
export const getContract = (id: number, token: string) => apiRequest<ContractDetails>(`/api/contracts/${id}`, {}, token);
export const createContract = (request: ContractRequest, token: string) => apiRequest<ContractDetails>('/api/contracts', { method: 'POST', body: JSON.stringify(request) }, token);
export const activateContract = (id: number, request: ActivateContractRequest, token: string) => apiRequest<ContractDetails>(`/api/contracts/${id}/activate`, { method: 'POST', body: JSON.stringify(request) }, token);
