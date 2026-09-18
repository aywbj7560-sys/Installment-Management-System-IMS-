import { useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getProduct } from '../api/products';
import { useAuth } from '../auth/AuthContext';
import { ActiveStatusBadge } from '../components/ActiveStatusBadge';
import type { Product } from '../types/products';
import { formatMoney } from '../utils/format';
import { canManageProducts } from '../utils/roles';

export function ProductDetailPage() {
  const { id } = useParams(); const location = useLocation(); const { token, user } = useAuth();
  const notice = (location.state as { notice?: string } | null)?.notice;
  const [product, setProduct] = useState<Product>(); const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ status: number; message: string }>();
  useEffect(() => { if (!token) return; getProduct(Number(id), token).then(setProduct).catch(reason => setError({ status: reason instanceof ApiError ? reason.status : 0, message: reason instanceof ApiError ? reason.message : 'Product details could not be loaded.' })).finally(() => setLoading(false)); }, [id, token]);
  if (loading) return <div className="inline-state">Loading product…</div>;
  if (error) return <div className="panel error-state"><p className="eyebrow">{error.status === 404 ? '404' : error.status === 403 ? '403' : 'Error'}</p><h1>{error.status === 404 ? 'Product Not Found' : error.status === 403 ? 'Access Denied' : 'Unable to load product'}</h1><p>{error.status === 404 ? 'The requested product does not exist.' : error.status === 403 ? 'You do not have permission to view this product.' : error.message}</p><Link to="/products">Back to products</Link></div>;
  if (!product) return null;
  return <section>{notice && <div className="success-notice" role="status">{notice}</div>}<div className="page-title"><div><p className="eyebrow">Product #{product.productId}</p><h1>{product.name}</h1></div><div className="page-actions">{user && canManageProducts(user.role) && <Link className="button-link primary" to={`/products/${product.productId}/edit`}>Edit product</Link>}<Link className="button-link secondary" to="/products">Back to products</Link></div></div><div className="panel details-grid"><Detail label="Product ID" value={String(product.productId)} /><Detail label="Status" value={<ActiveStatusBadge isActive={product.isActive} />} /><Detail label="Product code" value={product.productCode} /><Detail label="Created" value={new Date(product.createdAt).toLocaleString()} /><Detail label="Cash price" value={formatMoney(product.cashPrice)} /><Detail label="Installment price" value={product.installmentPrice === null ? '—' : formatMoney(product.installmentPrice)} /><Detail label="Description" value={product.description || '—'} wide /></div></section>;
}
function Detail({ label, value, wide }: { label: string; value: ReactNode; wide?: boolean }) { return <div className={wide ? 'detail-wide' : ''}><span>{label}</span><strong>{value}</strong></div>; }
