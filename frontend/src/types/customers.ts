export const customerStatuses = ['Active', 'Inactive', 'Blacklisted'] as const;
export type CustomerStatus = typeof customerStatuses[number];
export interface Customer {
  customerId: number;
  fullName: string;
  identificationNumber: string;
  phone: string;
  secondaryPhone: string | null;
  email: string | null;
  address: string;
  status: CustomerStatus;
  createdAt: string;
}
export interface CustomerPage { items: Customer[]; totalCount: number; page: number; pageSize: number }
export interface CustomerRequest {
  fullName: string;
  identificationNumber: string;
  phone: string;
  secondaryPhone: string | null;
  email: string | null;
  address: string;
  status: CustomerStatus;
}
export interface CustomerQuery { search?: string; status?: CustomerStatus; page?: number; pageSize?: number }
