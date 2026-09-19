import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import { activateContract } from '../api/contracts';
import type { ContractSummary } from '../types/contracts';
import { formatMoney } from '../utils/format';

export function ContractActivationDialog({ contract, token, onClose, onActivated, onConflict }: {
  contract: ContractSummary; token: string; onClose(): void; onActivated(): Promise<void>; onConflict(message: string): Promise<void>;
}) {
  const [confirmed, setConfirmed] = useState(false);
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (saving) return;
    if (!confirmed) { setError('Confirm receipt of the agreed down payment.'); return; }
    setSaving(true); setError('');
    try {
      await activateContract(contract.contractId, { downPaymentConfirmed: true, reference: reference.trim() || null, note: note.trim() || null }, token);
      await onActivated();
    } catch (reason) {
      const apiError = reason instanceof ApiError ? reason : undefined;
      if (apiError?.status === 409) await onConflict(apiError.message || 'The contract can no longer be activated.');
      else if (apiError?.status === 400) setError(Object.values(apiError.errors).flat()[0] || apiError.message || 'Check the activation details and try again.');
      else if (apiError?.status === 403) setError('Access Denied. You do not have permission to activate this contract.');
      else if (apiError?.status === 404) setError('Contract Not Found. It may have been removed.');
      else setError('Unable to activate the contract. Please try again.');
    } finally { setSaving(false); }
  };
  return <div className="modal-backdrop" role="presentation"><section className="activation-dialog" role="dialog" aria-modal="true" aria-labelledby="activation-title">
    <div className="activation-heading"><div><p className="eyebrow">Draft contract</p><h2 id="activation-title">Activate contract</h2></div><button type="button" className="text-button" disabled={saving} onClick={onClose}>Close</button></div>
    <p className="activation-warning">Activation confirms that the agreed down payment has been received. Activation does not create a payment transaction. The existing installment schedule will be preserved.</p>
    <dl className="activation-summary"><div><dt>Contract number</dt><dd>{contract.contractNumber}</dd></div><div><dt>Customer</dt><dd>{contract.customerName}</dd></div><div><dt>Total amount</dt><dd>{formatMoney(contract.totalAmount)}</dd></div><div><dt>Down payment</dt><dd>{formatMoney(contract.downPayment)}</dd></div><div><dt>Remaining amount</dt><dd>{formatMoney(contract.remainingAmount)}</dd></div><div><dt>Current status</dt><dd>Draft</dd></div></dl>
    <form className="activation-form" onSubmit={submit}>
      <label className="confirmation"><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> <span>I confirm that the agreed down payment has been received.</span></label>
      <label>Reference <span>(optional)</span><input value={reference} maxLength={100} onChange={event => setReference(event.target.value.slice(0, 100))} /></label>
      <label>Note <span>(optional)</span><textarea value={note} onChange={event => setNote(event.target.value)} /></label>
      {error && <div className="error" role="alert">{error}</div>}
      <div className="activation-actions"><button type="button" className="secondary" disabled={saving} onClick={onClose}>Cancel</button><button className="primary" disabled={saving || !confirmed}>{saving ? 'Activating…' : 'Activate contract'}</button></div>
    </form>
  </section></div>;
}
