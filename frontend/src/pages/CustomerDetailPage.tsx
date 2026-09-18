import { useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getCustomer } from '../api/customers';
import { useAuth } from '../auth/AuthContext';
import { CustomerStatusBadge } from '../components/CustomerStatusBadge';
import type { Customer } from '../types/customers';
import { canManageCustomers } from '../utils/roles';
export function CustomerDetailPage() {
  const { id } = useParams(); const location = useLocation(); const notice = (location.state as { notice?: string } | null)?.notice; const { token, user } = useAuth(); const [customer, setCustomer] = useState<Customer>(); const [loading, setLoading] = useState(true); const [error, setError] = useState<{ status: number; message: string }>();
  useEffect(() => { if (!token) return; getCustomer(Number(id), token).then(setCustomer).catch(err => setError({ status: err instanceof ApiError ? err.status : 0, message: err instanceof ApiError ? err.message : 'Customer details could not be loaded.' })).finally(() => setLoading(false)); }, [id, token]);
  if (loading) return <div className="inline-state">Loading customer...</div>;
  if (error) return <div className="panel error-state"><p className="eyebrow">{error.status === 404 ? '404' : error.status === 403 ? '403' : 'Error'}</p><h1>{error.status === 404 ? 'Customer not found' : error.status === 403 ? 'Access denied' : 'Unable to load customer'}</h1><p>{error.status === 404 ? 'The requested customer does not exist.' : error.message}</p><Link to="/customers">Back to customers</Link></div>;
  if (!customer) return null;
  return <section>{notice && <div className="success-notice" role="status">{notice}</div>}<div className="page-title"><div><p className="eyebrow">Customer #{customer.customerId}</p><h1>{customer.fullName}</h1></div><div className="page-actions">{user && canManageCustomers(user.role) && <Link className="button-link primary" to={`/customers/${customer.customerId}/edit`}>Edit customer</Link>}<Link className="button-link secondary" to="/customers">Back to customers</Link></div></div><div className="panel details-grid"><Detail label="Customer ID" value={String(customer.customerId)} /><Detail label="Status" value={<CustomerStatusBadge status={customer.status} />} /><Detail label="Full name" value={customer.fullName} /><Detail label="Identification number" value={customer.identificationNumber} /><Detail label="Phone" value={customer.phone} /><Detail label="Secondary phone" value={customer.secondaryPhone || '-'} /><Detail label="Email" value={customer.email || '-'} /><Detail label="Created" value={new Date(customer.createdAt).toLocaleString()} /><Detail label="Address" value={customer.address} wide /></div></section>;
}
function Detail({ label, value, wide }: { label: string; value: ReactNode; wide?: boolean }) { return <div className={wide ? 'detail-wide' : ''}><span>{label}</span><strong>{value}</strong></div>; }
