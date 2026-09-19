export const contractStatuses = ['Draft', 'Active', 'Completed', 'Voided', 'Defaulted'] as const;
export type ContractStatus = typeof contractStatuses[number];

export interface ContractSummary {
  contractId: number; contractNumber: string; customerId: number; customerName: string;
  createdByUserId: number; contractDate: string; totalAmount: number; downPayment: number;
  remainingAmount: number; numberOfInstallments: number; status: ContractStatus; createdAt: string;
}
export interface ContractItem { contractItemId: number; productId: number; productCode: string; productName: string; quantity: number; unitPrice: number; subtotal: number }
export interface ContractGuarantor {
  guarantorId: number; fullName: string; identificationNumber: string; phone: string;
  secondaryPhone: string | null; address: string; occupation: string | null; workplace: string | null;
  notes: string | null; isActive: boolean; guaranteeNotes: string | null;
}
export interface ContractInstallment { installmentId: number; installmentNumber: number; dueDate: string; amount: number; paidAmount: number; remainingAmount: number; status: string }
export interface ContractDetails { contract: ContractSummary; items: ContractItem[]; guarantors: ContractGuarantor[]; installments: ContractInstallment[] }
export interface ContractPage { items: ContractSummary[]; totalCount: number; page: number; pageSize: number }
export interface ContractQuery { search?: string; customerId?: number; status?: ContractStatus; page?: number; pageSize?: number }
export interface ContractItemRequest { productId: number; quantity: number; unitPrice: number }
export interface ContractRequest {
  contractNumber: string; customerId: number; contractDate: string; downPayment: number;
  items: ContractItemRequest[]; guarantorId: number; guarantor: null; guaranteeNotes: string | null;
}
