import type { Installment, InstallmentStatus } from './installments';

export type CollectionItem = Installment;
export interface CollectionPage { items: CollectionItem[]; totalCount: number; page: number; pageSize: number; asOfUtcDate: string }
export interface CollectionQuery { contractId?: number; customerId?: number; status?: InstallmentStatus; dueFrom?: string; dueTo?: string; pastDueOnly?: boolean; openOnly?: boolean; installmentNumber?: number; search?: string; page?: number; pageSize?: number }
