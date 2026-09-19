export interface Guarantor {
  guarantorId: number; fullName: string; identificationNumber: string; phone: string;
  secondaryPhone: string | null; address: string; occupation: string | null; workplace: string | null;
  notes: string | null; isActive: boolean; createdAt: string;
}
export interface GuarantorPage { items: Guarantor[]; totalCount: number; page: number; pageSize: number }
export interface GuarantorQuery { search?: string; isActive?: boolean; page?: number; pageSize?: number }
