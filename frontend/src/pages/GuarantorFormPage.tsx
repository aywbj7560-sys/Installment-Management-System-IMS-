import { useEffect, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { createGuarantor, getGuarantor, updateGuarantor } from '../api/guarantors';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { GuarantorForm } from '../components/GuarantorForm';
import type { GuarantorRequest } from '../types/guarantors';
import { canCreateGuarantors, canEditGuarantors } from '../utils/roles';

export function GuarantorFormPage({ editing = false }: { editing?: boolean }) {
  const { id } = useParams(); const { token, user } = useAuth(); const navigate = useNavigate(); const [initial, setInitial] = useState<GuarantorRequest>(); const [loading, setLoading] = useState(editing); const [loadError, setLoadError] = useState<{ status: number; message: string }>();
  useEffect(() => { if (!editing || !token) return; getGuarantor(Number(id), token).then(({ guarantor }) => setInitial({ fullName: guarantor.fullName, identificationNumber: guarantor.identificationNumber, phone: guarantor.phone, secondaryPhone: guarantor.secondaryPhone, address: guarantor.address, occupation: guarantor.occupation, workplace: guarantor.workplace, notes: guarantor.notes, isActive: guarantor.isActive })).catch(reason => { const status = reason instanceof ApiError ? reason.status : 0; setLoadError({ status, message: status === 404 ? 'Guarantor Not Found.' : 'The guarantor could not be loaded for editing.' }); }).finally(() => setLoading(false)); }, [editing, id, token]);
  if (!user || !(editing ? canEditGuarantors(user.role) : canCreateGuarantors(user.role))) return <Navigate to="/access-denied" replace />;
  if (loading) return <div className="inline-state">Loading guarantor…</div>;
  if (loadError) return <div className="panel error-state" role="alert"><h1>{loadError.status === 404 ? 'Guarantor Not Found' : 'Unable to edit guarantor'}</h1><p>{loadError.message}</p><Link to="/guarantors">Back to guarantors</Link></div>;
  const save = async (request: GuarantorRequest) => { const result = editing ? await updateGuarantor(Number(id), request, token!) : await createGuarantor(request, token!); navigate(`/guarantors/${result.guarantorId}`, { replace: true, state: { notice: editing ? 'Guarantor updated successfully.' : 'Guarantor created successfully.' } }); };
  return <section className="form-page"><div className="page-title"><div><p className="eyebrow">Guarantor management</p><h1>{editing ? 'Edit guarantor' : 'Create guarantor'}</h1><p>{editing ? 'Update contact, employment, and future eligibility information.' : 'Add a guarantor profile for future contract selection.'}</p></div><Link className="button-link secondary" to={editing ? `/guarantors/${id}` : '/guarantors'}>Cancel</Link></div><div className="panel"><GuarantorForm key={id || 'new'} initial={initial} editing={editing} onSubmit={save} /></div></section>;
}
