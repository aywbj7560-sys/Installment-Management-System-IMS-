import { useEffect, useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getInstallment } from '../api/installments';
import { useAuth } from '../auth/AuthContext';
import { ContractStatusBadge } from '../components/ContractStatusBadge';
import { InstallmentStatusBadge } from '../components/InstallmentStatusBadge';
import type { InstallmentDetails } from '../types/installments';
import { formatMoney } from '../utils/format';
import { canCreatePayments } from '../utils/roles';

export function InstallmentDetailPage() {
  const { id } = useParams(); const { token, user } = useAuth(); const [details, setDetails] = useState<InstallmentDetails>(); const [loading, setLoading] = useState(true); const [error, setError] = useState<number>();
  useEffect(() => { if (!token) return; setLoading(true); setError(undefined); getInstallment(Number(id), token).then(setDetails).catch(reason => setError(reason instanceof ApiError ? reason.status : 0)).finally(() => setLoading(false)); }, [id, token]);
  if (loading) return <div className="inline-state">Loading installment…</div>;
  if (error !== undefined) return <div className="panel error-state"><p className="eyebrow">{error === 404 ? '404' : error === 403 ? '403' : 'Error'}</p><h1>{error === 404 ? 'Installment Not Found' : error === 403 ? 'Access Denied' : 'Unable to load installment'}</h1><p>{error === 404 ? 'The requested installment does not exist.' : error === 403 ? 'You do not have permission to view this installment.' : 'Installment details could not be loaded. Please try again.'}</p><Link to="/installments">Back to installments</Link></div>;
  if (!details) return null; const item = details.installment;
  return <section><div className="page-title"><div><p className="eyebrow">Installment #{item.installmentId}</p><h1>Installment {item.installmentNumber}</h1><p>{item.contract.contractNumber} · {item.customer.fullName}</p></div><div className="page-actions">{item.contract.status === 'Active' && user && canCreatePayments(user.role) && <Link className="button-link primary" to={`/payments/new?contractId=${item.contract.contractId}`}>Record payment</Link>}<Link className="button-link secondary" to="/installments">Back to installments</Link></div></div>
    <Section title="Installment information"><div className="details-grid"><Detail label="Installment ID" value={String(item.installmentId)} /><Detail label="Installment number" value={String(item.installmentNumber)} /><Detail label="Due date" value={item.dueDate} /><Detail label="Status" value={<InstallmentStatusBadge status={item.status} />} /><Detail label="Past due" value={item.isPastDue ? 'Yes (reported by server)' : 'No'} /><Detail label="Open balance" value={item.isOpen ? 'Yes' : 'No'} /><Detail label="As of UTC date" value={details.asOfUtcDate} /></div></Section>
    <Section title="Amounts"><div className="details-grid"><Detail label="Original amount" value={formatMoney(item.amount)} /><Detail label="Paid amount" value={formatMoney(item.paidAmount)} /><Detail label="Remaining amount" value={formatMoney(item.remainingAmount)} /></div></Section>
    <Section title="Contract and customer"><div className="details-grid"><Detail label="Contract" value={<Link to={`/contracts/${item.contract.contractId}`}>{item.contract.contractNumber}</Link>} /><Detail label="Contract status" value={<ContractStatusBadge status={item.contract.status} />} /><Detail label="Customer" value={item.customer.fullName} /><Detail label="Customer ID" value={String(item.customer.customerId)} /><Detail label="Phone" value={item.customer.phone} /><Detail label="Secondary phone" value={item.customer.secondaryPhone || '—'} /></div></Section>
  </section>;
}
function Section({ title, children }: { title: string; children: ReactNode }) { return <section className="panel detail-section"><h2>{title}</h2>{children}</section>; }
function Detail({ label, value }: { label: string; value: ReactNode }) { return <div><span>{label}</span><strong>{value}</strong></div>; }
