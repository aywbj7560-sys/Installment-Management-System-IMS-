export type Role = 'Admin' | 'Financial Manager' | 'Sales Agent' | 'Collection Officer' | 'Auditor';
export interface CurrentUser { id: number; email: string; role: Role }
export interface LoginRequest { email: string; password: string }
export interface LoginResponse { token: string; expiresAtUtc: string; user: CurrentUser }
export interface ReportSummary { totalCustomers: number; totalContracts: number; totalPaymentsReceived: number; openInstallmentCount: number; openInstallmentBalance: number; asOfUtcDate: string }
