import { apiRequest } from './client';
import type { CreatePaymentRequest, PaymentDetails, PaymentPage, PaymentQuery } from '../types/payments';

export function getPayments(query: PaymentQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.contractId !== undefined) params.set('contractId', String(query.contractId));
  if (query.customerId !== undefined) params.set('customerId', String(query.customerId));
  params.set('page', String(query.page ?? 1)); params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<PaymentPage>(`/api/payments?${params}`, {}, token);
}
export const getPayment = (id: number, token: string) => apiRequest<PaymentDetails>(`/api/payments/${id}`, {}, token);
export const createPayment = (request: CreatePaymentRequest, token: string) => apiRequest<PaymentDetails>('/api/payments', { method: 'POST', body: JSON.stringify(request) }, token);
