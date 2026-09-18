import type { Role } from '../types/api';
export const canManageCustomers = (role: Role) => role === 'Admin' || role === 'Financial Manager' || role === 'Sales Agent';
