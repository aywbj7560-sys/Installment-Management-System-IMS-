import type { InstallmentStatus } from '../types/installments';
const classes: Record<InstallmentStatus, string> = { Pending: 'status-installment-pending', 'Partially Paid': 'status-installment-partial', Paid: 'status-installment-paid', Overdue: 'status-installment-overdue', Waived: 'status-installment-waived' };
export function InstallmentStatusBadge({ status }: { status: InstallmentStatus }) { return <span className={`status-badge ${classes[status]}`}>{status}</span>; }
