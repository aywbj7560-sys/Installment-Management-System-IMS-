import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import type { GuarantorRequest } from '../types/guarantors';

const blank: GuarantorRequest = { fullName: '', identificationNumber: '', phone: '', secondaryPhone: null, address: '', occupation: null, workplace: null, notes: null, isActive: true };
const phonePattern = /^\+?[0-9 ()-]*[0-9][0-9 ()-]*$/;
function fieldErrors(errors: Record<string, string[]>) { return Object.fromEntries(Object.entries(errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages])); }
export function GuarantorForm({ initial = blank, editing = false, onSubmit }: { initial?: GuarantorRequest; editing?: boolean; onSubmit(request: GuarantorRequest): Promise<void> }) {
  const [value, setValue] = useState(initial); const [errors, setErrors] = useState<Record<string, string[]>>({}); const [message, setMessage] = useState(''); const [saving, setSaving] = useState(false);
  const set = (name: keyof GuarantorRequest, next: string | boolean) => setValue(old => ({ ...old, [name]: next }));
  const submit = async (event: FormEvent) => {
    event.preventDefault(); if (saving) return; const next: Record<string, string[]> = {};
    if (!value.fullName.trim()) next.fullName = ['Full name is required.']; if (!value.identificationNumber.trim()) next.identificationNumber = ['Identification number is required.']; if (!value.phone.trim()) next.phone = ['Phone is required.']; else if (!phonePattern.test(value.phone.trim())) next.phone = ['Use digits, spaces, parentheses, hyphens and an optional leading +.']; if (value.secondaryPhone?.trim() && !phonePattern.test(value.secondaryPhone.trim())) next.secondaryPhone = ['Use digits, spaces, parentheses, hyphens and an optional leading +.']; if (!value.address.trim()) next.address = ['Address is required.'];
    if (Object.keys(next).length) { setErrors(next); setMessage('Please correct the highlighted guarantor fields.'); return; }
    setSaving(true); setErrors({}); setMessage('');
    const request: GuarantorRequest = { fullName: value.fullName.trim(), identificationNumber: value.identificationNumber.trim(), phone: value.phone.trim(), secondaryPhone: value.secondaryPhone?.trim() || null, address: value.address.trim(), occupation: value.occupation?.trim() || null, workplace: value.workplace?.trim() || null, notes: value.notes?.trim() || null, isActive: value.isActive };
    try { await onSubmit(request); }
    catch (reason) { const apiError = reason instanceof ApiError ? reason : undefined; if (apiError?.status === 400) { setErrors(fieldErrors(apiError.errors)); setMessage('Please correct the highlighted guarantor fields.'); } else if (apiError?.status === 403) setMessage('Access Denied. You do not have permission to save guarantors.'); else if (apiError?.status === 404) setMessage('Guarantor Not Found. It may have been removed.'); else if (apiError?.status === 409) setMessage(apiError.message || 'The guarantor conflicts with existing data.'); else setMessage('Unable to save the guarantor. Please try again.'); } finally { setSaving(false); }
  };
  return <form className="customer-form guarantor-form" onSubmit={submit} noValidate>{message && <div className="error" role="alert">{message}</div>}<div className="form-grid">
    <Field label="Full name" name="fullName" value={value.fullName} error={errors.fullName} readOnly={editing} maxLength={150} onChange={set} />
    <Field label="Identification number" name="identificationNumber" value={value.identificationNumber} error={errors.identificationNumber} readOnly={editing} maxLength={50} onChange={set} />
    <Field label="Phone" name="phone" value={value.phone} error={errors.phone} maxLength={20} onChange={set} />
    <Field label="Secondary phone" name="secondaryPhone" value={value.secondaryPhone ?? ''} error={errors.secondaryPhone} maxLength={20} onChange={set} />
    <Field label="Occupation" name="occupation" value={value.occupation ?? ''} error={errors.occupation} maxLength={100} onChange={set} />
    <Field label="Workplace" name="workplace" value={value.workplace ?? ''} error={errors.workplace} maxLength={150} onChange={set} />
    <label>Status<select aria-label="Status" value={value.isActive ? 'true' : 'false'} onChange={event => set('isActive', event.target.value === 'true')}><option value="true">Active</option><option value="false">Inactive</option></select>{!value.isActive && <small className="field-warning">Inactive guarantors cannot be selected for new contracts. Historical contract links remain unchanged.</small>}</label>
    <label className="full-field">Address<textarea value={value.address} onChange={event => set('address', event.target.value)} />{errors.address && <small className="field-error">{errors.address[0]}</small>}</label>
    <label className="full-field">Notes <span>(optional)</span><textarea value={value.notes ?? ''} onChange={event => set('notes', event.target.value)} />{errors.notes && <small className="field-error">{errors.notes[0]}</small>}</label>
  </div><button className="primary" disabled={saving}>{saving ? 'Saving…' : editing ? 'Save changes' : 'Create guarantor'}</button></form>;
}
function Field({ label, name, value, error, readOnly, maxLength, onChange }: { label: string; name: keyof GuarantorRequest; value: string; error?: string[]; readOnly?: boolean; maxLength: number; onChange(name: keyof GuarantorRequest, value: string): void }) { return <label>{label}<input aria-invalid={!!error} value={value} readOnly={readOnly} maxLength={maxLength} onChange={event => onChange(name, event.target.value)} />{readOnly && <small>Identity fields cannot be changed.</small>}{error && <small className="field-error">{error[0]}</small>}</label>; }
