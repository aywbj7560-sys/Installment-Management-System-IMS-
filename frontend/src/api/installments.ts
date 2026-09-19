import { apiRequest } from './client';
import type { ContractInstallmentSchedule, InstallmentDetails, InstallmentPage, InstallmentQuery } from '../types/installments';

export function getInstallments(query: InstallmentQuery, token: string) {
  const params = new URLSearchParams();
  if (query.contractId !== undefined) params.set('contractId', String(query.contractId));
  if (query.customerId !== undefined) params.set('customerId', String(query.customerId));
  if (query.status) params.set('status', query.status);
  if (query.dueFrom) params.set('dueFrom', query.dueFrom);
  if (query.dueTo) params.set('dueTo', query.dueTo);
  if (query.pastDueOnly) params.set('pastDueOnly', 'true');
  if (query.openOnly) params.set('openOnly', 'true');
  if (query.installmentNumber !== undefined) params.set('installmentNumber', String(query.installmentNumber));
  if (query.search) params.set('search', query.search);
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<InstallmentPage>(`/api/installments?${params}`, {}, token);
}
export const getInstallment = (id: number, token: string) => apiRequest<InstallmentDetails>(`/api/installments/${id}`, {}, token);
export const getContractInstallments = (contractId: number, token: string) => apiRequest<ContractInstallmentSchedule>(`/api/contracts/${contractId}/installments`, {}, token);
