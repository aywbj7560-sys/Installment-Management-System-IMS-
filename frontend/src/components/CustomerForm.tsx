import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import { customerStatuses, type CustomerRequest } from '../types/customers';
type Props = { initial?: CustomerRequest; editing?: boolean; onSubmit(value: CustomerRequest): Promise<void> };
const blank: CustomerRequest = { fullName: '', identificationNumber: '', phone: '', secondaryPhone: null, email: null, address: '', status: 'Active' };
export function CustomerForm({ initial = blank, editing = false, onSubmit }: Props) {
  const [value, setValue] = useState(initial); const [errors, setErrors] = useState<Record<string, string[]>>({}); const [message, setMessage] = useState(''); const [saving, setSaving] = useState(false);
  const set = (name: keyof CustomerRequest, next: string) => setValue(old => ({ ...old, [name]: next }));
  const submit = async (event: FormEvent) => { event.preventDefault(); const next: Record<string, string[]> = {}; if (!value.fullName.trim()) next.fullName = ['Full name is required.']; if (!value.identificationNumber.trim()) next.identificationNumber = ['Identification number is required.']; if (!value.phone.trim()) next.phone = ['Phone is required.']; if (!value.address.trim()) next.address = ['Address is required.']; if (Object.keys(next).length) { setErrors(next); return; } setSaving(true); setErrors({}); setMessage(''); try { await onSubmit({ ...value, secondaryPhone: value.secondaryPhone?.trim() || null, email: value.email?.trim() || null }); } catch (error) { if (error instanceof ApiError) { setErrors(Object.fromEntries(Object.entries(error.errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages]))); setMessage(error.status === 409 ? error.message : error.status === 400 ? 'Please correct the highlighted fields.' : error.message); } else setMessage('The customer could not be saved.'); } finally { setSaving(false); } };
  return <form className="customer-form" onSubmit={submit} noValidate>{message && <div className="error" role="alert">{message}</div>}<div className="form-grid">
    <Field label="Full name" name="fullName" value={value.fullName} error={errors.fullName} readOnly={editing} maxLength={150} onChange={set} />
    <Field label="Identification number" name="identificationNumber" value={value.identificationNumber} error={errors.identificationNumber} readOnly={editing} maxLength={50} onChange={set} />
    <Field label="Phone" name="phone" value={value.phone} error={errors.phone} maxLength={20} onChange={set} />
    <Field label="Secondary phone" name="secondaryPhone" value={value.secondaryPhone ?? ''} error={errors.secondaryPhone} maxLength={20} onChange={set} />
    <Field label="Email" name="email" type="email" value={value.email ?? ''} error={errors.email} maxLength={100} onChange={set} />
    <label>Status<select value={value.status} onChange={e => set('status', e.target.value)}>{customerStatuses.map(status => <option key={status}>{status}</option>)}</select></label>
    <label className="full-field">Address<textarea value={value.address} onChange={e => set('address', e.target.value)} />{errors.address && <small className="field-error">{errors.address[0]}</small>}</label>
  </div><button className="primary" disabled={saving}>{saving ? 'Saving…' : editing ? 'Save changes' : 'Create customer'}</button></form>;
}
function Field({ label, name, value, error, readOnly, type = 'text', maxLength, onChange }: { label: string; name: keyof CustomerRequest; value: string; error?: string[]; readOnly?: boolean; type?: string; maxLength: number; onChange(name: keyof CustomerRequest, value: string): void }) { return <label>{label}<input aria-invalid={!!error} type={type} value={value} readOnly={readOnly} maxLength={maxLength} onChange={e => onChange(name, e.target.value)} />{readOnly && <small>Identity fields cannot be changed.</small>}{error && <small className="field-error">{error[0]}</small>}</label>; }
