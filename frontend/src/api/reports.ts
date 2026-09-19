import { apiRequest } from './client';
import type { ContractReportPage, ContractStatement, CustomerStatement, OutstandingQuery, OutstandingReportPage, PaymentReportPage, ReportQuery, ReportSummary, StatementQuery } from '../types/reports';

function params(values: Record<string, string | number | boolean | undefined>) { const result = new URLSearchParams(); Object.entries(values).forEach(([key, value]) => { if (value !== undefined && value !== '') result.set(key, String(value)); }); return result; }
export const getReportSummary = (token: string) => apiRequest<ReportSummary>('/api/reports/summary', {}, token);
export const getContractsReport = (query: ReportQuery, token: string) => apiRequest<ContractReportPage>(`/api/reports/contracts?${params({ ...query })}`, {}, token);
export const getPaymentsReport = (query: ReportQuery, token: string) => apiRequest<PaymentReportPage>(`/api/reports/payments?${params({ ...query })}`, {}, token);
export const getOutstandingReport = (query: OutstandingQuery, token: string) => apiRequest<OutstandingReportPage>(`/api/reports/outstanding?${params({ ...query })}`, {}, token);
export const getCustomerStatement = (customerId: number, query: StatementQuery, token: string) => apiRequest<CustomerStatement>(`/api/reports/customers/${customerId}/statement?${params({ ...query })}`, {}, token);
export const getContractStatement = (contractId: number, query: StatementQuery, token: string) => apiRequest<ContractStatement>(`/api/reports/contracts/${contractId}/statement?${params({ ...query })}`, {}, token);
