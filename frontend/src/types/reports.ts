import type { ContractStatus } from './contracts';
import type { Installment, InstallmentStatus } from './installments';

export interface ReportPage<T> { items: T[]; totalCount: number; page: number; pageSize: number; asOfUtcDate: string }
export interface ReportCustomer { customerId: number; fullName: string; phone: string; status: string }
export interface ContractReportItem { contractId: number; contractNumber: string; customer: ReportCustomer; contractDate: string; totalAmount: number; downPayment: number; remainingAmount: number; status: ContractStatus; numberOfInstallments: number; paidInstallmentCount: number; openInstallmentCount: number }
export interface PaymentReportItem { paymentId: number; paymentReference: string; paymentDate: string; amount: number; paymentMethod: string; contractId: number; contractNumber: string; customer: ReportCustomer; receivedByUserId: number; receivedByName: string; allocatedTotal: number }
export interface ReportSummary { totalCustomers: number; activeCustomers: number; totalProducts: number; activeProducts: number; totalContracts: number; contractsByStatus: Record<string, number>; completedContracts: number; totalContractValue: number; totalFinancedPrincipal: number; totalContractRemaining: number; activeContractRemaining: number; totalPaymentsReceived: number; openInstallmentCount: number; openInstallmentBalance: number; pastDueInstallmentCount: number; pastDueInstallmentBalance: number; asOfUtcDate: string }
export interface StatementTotals { contractRemaining: number; installmentRemaining: number; installmentPaid: number; paymentsReceived: number; allocationsTotal: number }
export interface StatementItem { contractItemId: number; productId: number; productCode: string; productName: string; quantity: number; unitPrice: number; subtotal: number }
export interface StatementGuarantor { contractGuarantorId: number; guarantorId: number; fullName: string; phone: string; isActive: boolean; guaranteeNotes: string | null }
export interface CustomerStatement { customer: ReportCustomer; contracts: ReportPage<ContractReportItem>; installments: ReportPage<Installment>; payments: ReportPage<PaymentReportItem>; totals: StatementTotals; asOfUtcDate: string }
export interface ContractStatement { contract: ContractReportItem; items: ReportPage<StatementItem>; guarantors: ReportPage<StatementGuarantor>; installments: Installment[]; payments: ReportPage<PaymentReportItem>; totals: StatementTotals; asOfUtcDate: string }
export interface ReportQuery { customerId?: number; contractId?: number; status?: ContractStatus; dateFrom?: string; dateTo?: string; paymentMethod?: string; search?: string; page?: number; pageSize?: number }
export interface OutstandingQuery { contractId?: number; customerId?: number; status?: InstallmentStatus; dueFrom?: string; dueTo?: string; pastDueOnly?: boolean; installmentNumber?: number; search?: string; page?: number; pageSize?: number }
export interface StatementQuery { contractsPage?: number; installmentsPage?: number; paymentsPage?: number; itemsPage?: number; guarantorsPage?: number; pageSize?: number }
export type ContractReportPage = ReportPage<ContractReportItem>;
export type PaymentReportPage = ReportPage<PaymentReportItem>;
export type OutstandingReportPage = ReportPage<Installment>;
