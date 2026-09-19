import type { ContractInstallment, ContractStatus } from './contracts';

export interface Payment {
  paymentId: number; paymentReference: string; contractId: number; receivedByUserId: number;
  paymentDate: string; amount: number; paymentMethod: string; notes: string | null;
}
export interface PaymentContract {
  contractId: number; contractNumber: string; customerId: number; customerName: string;
  remainingAmount: number; status: ContractStatus;
}
export interface PaymentAllocation {
  paymentAllocationId: number; allocatedAmount: number; createdAt: string; installment: ContractInstallment;
}
export interface PaymentDetails { payment: Payment; contract: PaymentContract; allocations: PaymentAllocation[] }
export interface PaymentPage { items: Payment[]; totalCount: number; page: number; pageSize: number }
export interface PaymentQuery { search?: string; contractId?: number; customerId?: number; page?: number; pageSize?: number }
export interface CreatePaymentRequest {
  contractId: number; paymentReference: string; paymentDate: string | null;
  amount: number; paymentMethod: string; notes: string | null;
}
