import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import type { ProductRequest } from '../types/products';

export interface ProductFormValue { productCode: string; name: string; description: string; cashPrice: string; installmentPrice: string; isActive: boolean }
type Props = { initial?: ProductFormValue; editing?: boolean; onSubmit(value: ProductRequest): Promise<void> };
const blank: ProductFormValue = { productCode: '', name: '', description: '', cashPrice: '', installmentPrice: '', isActive: true };
const pricePattern = /^\d+(?:\.\d{1,2})?$/;

function validPrice(value: string) {
  if (!pricePattern.test(value)) return false;
  const [whole] = value.split('.');
  const normalized = whole.replace(/^0+(?=\d)/, '');
  return normalized.length < 13 || (normalized.length === 13 && normalized <= '9999999999999');
}
function normalizeErrors(errors: Record<string, string[]>) {
  return Object.fromEntries(Object.entries(errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages]));
}

export function ProductForm({ initial = blank, editing = false, onSubmit }: Props) {
  const [value, setValue] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (name: keyof ProductFormValue, next: string | boolean) => setValue(old => ({ ...old, [name]: next }));
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const next: Record<string, string[]> = {};
    if (!value.productCode.trim()) next.productCode = ['Product code is required.'];
    if (!value.name.trim()) next.name = ['Name is required.'];
    if (!value.cashPrice.trim()) next.cashPrice = ['Cash price is required.'];
    else if (!validPrice(value.cashPrice.trim())) next.cashPrice = ['Use a price from 0 to 9999999999999.99 with at most two decimal places.'];
    if (value.installmentPrice.trim() && !validPrice(value.installmentPrice.trim())) next.installmentPrice = ['Use a price from 0 to 9999999999999.99 with at most two decimal places.'];
    if (Object.keys(next).length) { setErrors(next); setMessage(''); return; }
    setSaving(true); setErrors({}); setMessage('');
    try {
      await onSubmit({ productCode: value.productCode.trim(), name: value.name.trim(), description: value.description.trim() || null, cashPrice: Number(value.cashPrice), installmentPrice: value.installmentPrice.trim() ? Number(value.installmentPrice) : null, isActive: value.isActive });
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(normalizeErrors(error.errors));
        setMessage(error.status === 409 ? 'A product with this product code already exists.' : error.status === 400 ? 'Please correct the highlighted fields.' : error.status === 403 ? 'You do not have permission to save products.' : error.message);
      } else setMessage('The product could not be saved.');
    } finally { setSaving(false); }
  };
  return <form className="customer-form" onSubmit={submit} noValidate>{message && <div className="error" role="alert">{message}</div>}<div className="form-grid">
    <Field label="Product code" value={value.productCode} error={errors.productCode} maxLength={50} onChange={next => set('productCode', next)} />
    <Field label="Name" value={value.name} error={errors.name} maxLength={150} onChange={next => set('name', next)} />
    <Field label="Cash price" value={value.cashPrice} error={errors.cashPrice} inputMode="decimal" onChange={next => set('cashPrice', next)} />
    <Field label="Installment price" value={value.installmentPrice} error={errors.installmentPrice} inputMode="decimal" onChange={next => set('installmentPrice', next)} />
    <label className="full-field">Description<textarea value={value.description} onChange={e => set('description', e.target.value)} />{errors.description && <small className="field-error">{errors.description[0]}</small>}</label>
    <label className="checkbox-field"><input type="checkbox" checked={value.isActive} onChange={e => set('isActive', e.target.checked)} /> Active</label>
  </div><button className="primary" disabled={saving}>{saving ? 'Saving…' : editing ? 'Save changes' : 'Create product'}</button></form>;
}

function Field({ label, value, error, maxLength, inputMode, onChange }: { label: string; value: string; error?: string[]; maxLength?: number; inputMode?: 'decimal'; onChange(value: string): void }) {
  return <label>{label}<input aria-invalid={!!error} value={value} maxLength={maxLength} inputMode={inputMode} onChange={e => onChange(e.target.value)} />{error && <small className="field-error">{error[0]}</small>}</label>;
}
