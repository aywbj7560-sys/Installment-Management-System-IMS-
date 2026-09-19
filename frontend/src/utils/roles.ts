import type { Role } from '../types/api';
export const canManageCustomers = (role: Role) => role === 'Admin' || role === 'Financial Manager' || role === 'Sales Agent';
export const canManageProducts = (role: Role) => role === 'Admin' || role === 'Financial Manager';
export const canCreateContracts = (role: Role) => role === 'Admin' || role === 'Financial Manager' || role === 'Sales Agent';
export const canActivateContracts = (role: Role) => role === 'Admin' || role === 'Financial Manager';
