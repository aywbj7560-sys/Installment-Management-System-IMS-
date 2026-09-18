import type { CustomerStatus } from '../types/customers';
export function CustomerStatusBadge({ status }: { status: CustomerStatus }) { return <span className={`status-badge status-${status.toLowerCase()}`}>{status}</span>; }
