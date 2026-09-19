import { useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getGuarantor } from '../api/guarantors';
import { useAuth } from '../auth/AuthContext';
import { ActiveStatusBadge } from '../components/ActiveStatusBadge';
import type { GuarantorDetails } from '../types/guarantors';
import { canEditGuarantors } from '../utils/roles';

export function GuarantorDetailPage() {
  const { id } = useParams(); const location = useLocation(); const notice = (location.state as { notice?: string } | null)?.notice; const { token, user } = useAuth(); const [details, setDetails] = useState<GuarantorDetails>(); const [loading, setLoading] = useState(true); const [error, setError] = useState<{ status: number; message: string }>();
  useEffect(() => { if (!token) return; getGuarantor(Number(id), token).then(setDetails).catch(reason => setError({ status: reason instanceof ApiError ? reason.status : 0, message: reason instanceof ApiError ? reason.message : 'Guarantor details could not be loaded.' })).finally(() => setLoading(false)); }, [id, token]);
  if (loading) return <div className="inline-state">Loading guarantor…</div>;
  if (error) return <div className="panel error-state"><p className="eyebrow">{error.status === 404 ? '404' : error.status === 403 ? '403' : 'Error'}</p><h1>{error.status === 404 ? 'Guarantor Not Found' : error.status === 403 ? 'Access Denied' : 'Unable to load guarantor'}</h1><p>{error.status === 404 ? 'The requested guarantor does not exist.' : error.status === 403 ? 'You do not have permission to view this guarantor.' : 'Guarantor details could not be loaded. Please try again.'}</p><Link to="/guarantors">Back to guarantors</Link></div>;
  if (!details) return null; const guarantor = details.guarantor;
  return <section>{notice && <div className="success-notice" role="status">{notice}</div>}<div className="page-title"><div><p className="eyebrow">Guarantor #{guarantor.guarantorId}</p><h1>{guarantor.fullName}</h1><p>{guarantor.identificationNumber}</p></div><div className="page-actions">{user && canEditGuarantors(user.role) && <Link className="button-link primary" to={`/guarantors/${guarantor.guarantorId}/edit`}>Edit guarantor</Link>}<Link className="button-link secondary" to="/guarantors">Back to guarantors</Link></div></div>
    <section className="panel detail-section"><h2>Profile</h2><div className="details-grid"><Detail label="Status" value={<ActiveStatusBadge isActive={guarantor.isActive} />} /><Detail label="Created" value={new Date(guarantor.createdAt).toLocaleString()} /><Detail label="Full name" value={guarantor.fullName} /><Detail label="Identification number" value={guarantor.identificationNumber} /><Detail label="Phone" value={guarantor.phone} /><Detail label="Secondary phone" value={guarantor.secondaryPhone || '—'} /><Detail label="Occupation" value={guarantor.occupation || '—'} /><Detail label="Workplace" value={guarantor.workplace || '—'} /><Detail label="Address" value={guarantor.address} wide /><Detail label="Notes" value={guarantor.notes || '—'} wide /></div></section>
    <section className="panel detail-section"><h2>Historical contract links</h2>{details.contracts.length === 0 ? <p className="detail-empty">No contracts are linked to this guarantor.</p> : <div className="table-scroll"><table><thead><tr><th>Contract</th><th>Status</th><th>Guarantee notes</th><th>Linked</th></tr></thead><tbody>{details.contracts.map(link => <tr key={link.contractGuarantorId}><td><Link to={`/contracts/${link.contractId}`}>{link.contractNumber}</Link></td><td>{link.status}</td><td>{link.guaranteeNotes || '—'}</td><td>{new Date(link.linkedAt).toLocaleString()}</td></tr>)}</tbody></table></div>}</section>
  </section>;
}
function Detail({ label, value, wide }: { label: string; value: ReactNode; wide?: boolean }) { return <div className={wide ? 'detail-wide' : ''}><span>{label}</span><strong>{value}</strong></div>; }
