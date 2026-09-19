import type { ContractStatus } from './contracts';

export const installmentStatuses = ['Pending', 'Partially Paid', 'Paid', 'Overdue', 'Waived'] as const;
export type InstallmentStatus = typeof installmentStatuses[number];
export interface InstallmentContractSummary { contractId: number; contractNumber: string; status: ContractStatus }
export interface InstallmentCustomerSummary { customerId: number; fullName: string; phone: string; secondaryPhone: string | null }
export interface Installment { installmentId: number; installmentNumber: number; dueDate: string; amount: number; paidAmount: number; remainingAmount: number; status: InstallmentStatus; isPastDue: boolean; isOpen: boolean; contract: InstallmentContractSummary; customer: InstallmentCustomerSummary }
export interface InstallmentPage { items: Installment[]; totalCount: number; page: number; pageSize: number; asOfUtcDate: string }
export interface InstallmentDetails { installment: Installment; asOfUtcDate: string }
export interface ContractInstallmentSchedule { contractId: number; items: Installment[]; asOfUtcDate: string }
export interface InstallmentQuery { contractId?: number; customerId?: number; status?: InstallmentStatus; dueFrom?: string; dueTo?: string; pastDueOnly?: boolean; openOnly?: boolean; installmentNumber?: number; search?: string; page?: number; pageSize?: number }
