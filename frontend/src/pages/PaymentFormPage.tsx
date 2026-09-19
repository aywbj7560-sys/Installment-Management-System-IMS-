import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { createPayment } from '../api/payments';
import { useAuth } from '../auth/AuthContext';
import { PaymentForm } from '../components/PaymentForm';
import type { CreatePaymentRequest } from '../types/payments';
import { canCreatePayments } from '../utils/roles';

export function PaymentFormPage() {
  const { token, user } = useAuth(); const navigate = useNavigate(); const [params] = useSearchParams(); const rawContractId = params.get('contractId'); const initialContractId = rawContractId && /^\d+$/.test(rawContractId) && Number(rawContractId) > 0 ? Number(rawContractId) : undefined;
  if (!user || !canCreatePayments(user.role)) return <Navigate to="/access-denied" replace />;
  const save = async (request: CreatePaymentRequest) => { const result = await createPayment(request, token!); navigate(`/payments/${result.payment.paymentId}`, { replace: true, state: { notice: 'Payment recorded successfully. Server allocation is shown below.' } }); };
  return <section className="form-page contract-form-page"><div className="page-title"><div><p className="eyebrow">Payment management</p><h1>Record payment</h1><p>Record a receipt for an Active contract. The backend controls allocation and balance updates.</p></div><Link className="button-link secondary" to="/payments">Cancel</Link></div><div className="panel"><PaymentForm token={token!} initialContractId={initialContractId} onSubmit={save} /></div></section>;
}
