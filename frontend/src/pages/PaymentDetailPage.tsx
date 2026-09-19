import { useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate, useLocation, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getPayment } from '../api/payments';
import { useAuth } from '../auth/AuthContext';
import { ContractStatusBadge } from '../components/ContractStatusBadge';
import type { PaymentDetails } from '../types/payments';
import { formatMoney } from '../utils/format';
import { canReadPayments } from '../utils/roles';

export function PaymentDetailPage() {
  const { id } = useParams(); const location = useLocation(); const { token, user } = useAuth(); const notice = (location.state as { notice?: string } | null)?.notice; const [details, setDetails] = useState<PaymentDetails>(); const [loading, setLoading] = useState(true); const [error, setError] = useState<{ status: number; message: string }>();
  useEffect(() => { if (!token || !user || !canReadPayments(user.role)) return; setLoading(true); getPayment(Number(id), token).then(setDetails).catch(reason => setError({ status: reason instanceof ApiError ? reason.status : 0, message: reason instanceof ApiError ? reason.message : 'Payment details could not be loaded.' })).finally(() => setLoading(false)); }, [id, token, user]);
  if (!user || !canReadPayments(user.role)) return <Navigate to="/access-denied" replace />;
  if (loading) return <div className="inline-state">Loading payment…</div>;
  if (error) return <div className="panel error-state"><p className="eyebrow">{error.status === 404 ? '404' : error.status === 403 ? '403' : 'Error'}</p><h1>{error.status === 404 ? 'Payment Not Found' : error.status === 403 ? 'Access Denied' : 'Unable to load payment'}</h1><p>{error.status === 404 ? 'The requested payment does not exist.' : error.status === 403 ? 'You do not have permission to view this payment.' : 'Payment details could not be loaded. Please try again.'}</p><Link to="/payments">Back to payments</Link></div>;
  if (!details) return null; const { payment, contract, allocations } = details;
  return <section>{notice && <div className="success-notice" role="status">{notice}</div>}<div className="page-title"><div><p className="eyebrow">Payment #{payment.paymentId}</p><h1>{payment.paymentReference}</h1><p>{contract.contractNumber} · {contract.customerName}</p></div><Link className="button-link secondary" to="/payments">Back to payments</Link></div>
    <Section title="Payment summary"><div className="details-grid"><Detail label="Amount" value={formatMoney(payment.amount)} /><Detail label="Payment date" value={new Date(payment.paymentDate).toLocaleString()} /><Detail label="Payment method" value={payment.paymentMethod} /><Detail label="Received by user ID" value={String(payment.receivedByUserId)} /><Detail label="Notes" value={payment.notes || '—'} wide /></div></Section>
    <Section title="Contract"><div className="details-grid"><Detail label="Contract" value={<Link to={`/contracts/${contract.contractId}`}>{contract.contractNumber}</Link>} /><Detail label="Customer" value={contract.customerName} /><Detail label="Remaining amount" value={formatMoney(contract.remainingAmount)} /><Detail label="Current status" value={<ContractStatusBadge status={contract.status} />} /></div></Section>
    <Section title="Server allocation"><p className="allocation-note">These are the allocations returned by the server for this receipt.</p><div className="table-scroll"><table><thead><tr><th>Installment</th><th>Due date</th><th>Allocated</th><th>Installment amount</th><th>Paid</th><th>Remaining</th><th>Status</th></tr></thead><tbody>{allocations.map(allocation => <tr key={allocation.paymentAllocationId}><td>#{allocation.installment.installmentNumber}</td><td>{allocation.installment.dueDate}</td><td className="numeric">{formatMoney(allocation.allocatedAmount)}</td><td className="numeric">{formatMoney(allocation.installment.amount)}</td><td className="numeric">{formatMoney(allocation.installment.paidAmount)}</td><td className="numeric">{formatMoney(allocation.installment.remainingAmount)}</td><td>{allocation.installment.status}</td></tr>)}</tbody></table></div></Section>
  </section>;
}
function Section({ title, children }: { title: string; children: ReactNode }) { return <section className="panel detail-section"><h2>{title}</h2>{children}</section>; }
function Detail({ label, value, wide }: { label: string; value: ReactNode; wide?: boolean }) { return <div className={wide ? 'detail-wide' : ''}><span>{label}</span><strong>{value}</strong></div>; }
