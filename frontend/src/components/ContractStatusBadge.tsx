import type { ContractStatus } from '../types/contracts';
export function ContractStatusBadge({ status }: { status: ContractStatus }) { return <span className={`status-badge status-contract-${status.toLowerCase()}`}>{status}</span>; }
