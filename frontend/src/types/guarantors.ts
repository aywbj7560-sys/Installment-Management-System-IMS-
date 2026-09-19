export interface Guarantor {
  guarantorId: number; fullName: string; identificationNumber: string; phone: string;
  secondaryPhone: string | null; address: string; occupation: string | null; workplace: string | null;
  notes: string | null; isActive: boolean; createdAt: string;
}
export interface GuarantorPage { items: Guarantor[]; totalCount: number; page: number; pageSize: number }
export interface GuarantorQuery { search?: string; isActive?: boolean; page?: number; pageSize?: number }
export interface GuarantorContractSummary {
  contractGuarantorId: number; contractId: number; contractNumber: string; status: string;
  guaranteeNotes: string | null; linkedAt: string;
}
export interface GuarantorDetails { guarantor: Guarantor; contracts: GuarantorContractSummary[] }
export interface GuarantorRequest {
  fullName: string; identificationNumber: string; phone: string; secondaryPhone: string | null;
  address: string; occupation: string | null; workplace: string | null; notes: string | null; isActive: boolean | null;
}
