import { useEffect, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { createProduct, getProduct, updateProduct } from '../api/products';
import { useAuth } from '../auth/AuthContext';
import { ProductForm, type ProductFormValue } from '../components/ProductForm';
import type { ProductRequest } from '../types/products';
import { canManageProducts } from '../utils/roles';

export function ProductFormPage({ editing = false }: { editing?: boolean }) {
  const { id } = useParams(); const { token, user } = useAuth(); const navigate = useNavigate();
  const [initial, setInitial] = useState<ProductFormValue>(); const [loading, setLoading] = useState(editing);
  const [loadError, setLoadError] = useState<{ status: number; message: string }>();
  useEffect(() => {
    if (!editing || !token) return;
    getProduct(Number(id), token).then(product => setInitial({ productCode: product.productCode, name: product.name, description: product.description ?? '', cashPrice: String(product.cashPrice), installmentPrice: product.installmentPrice === null ? '' : String(product.installmentPrice), isActive: product.isActive }))
      .catch(reason => setLoadError({ status: reason instanceof ApiError ? reason.status : 0, message: reason instanceof ApiError ? reason.message : 'The product could not be loaded for editing.' })).finally(() => setLoading(false));
  }, [editing, id, token]);
  if (!user || !canManageProducts(user.role)) return <Navigate to="/access-denied" replace />;
  if (loading) return <div className="inline-state">Loading product…</div>;
  if (loadError) return <div className="panel error-state" role="alert"><h1>{loadError.status === 404 ? 'Product Not Found' : loadError.status === 403 ? 'Access Denied' : 'Unable to edit product'}</h1><p>{loadError.status === 404 ? 'The requested product does not exist.' : loadError.status === 403 ? 'You do not have permission to edit this product.' : loadError.message}</p><Link to="/products">Back to products</Link></div>;
  const save = async (request: ProductRequest) => { const result = editing ? await updateProduct(Number(id), request, token!) : await createProduct(request, token!); navigate(`/products/${result.productId}`, { replace: true, state: { notice: editing ? 'Product updated successfully.' : 'Product created successfully.' } }); };
  return <section className="form-page"><div className="page-title"><div><p className="eyebrow">Product management</p><h1>{editing ? 'Edit product' : 'Create product'}</h1><p>{editing ? 'Update product details, pricing, and availability.' : 'Add a product with its cash and optional installment price.'}</p></div><Link className="button-link secondary" to={editing ? `/products/${id}` : '/products'}>Cancel</Link></div><div className="panel"><ProductForm key={id || 'new'} initial={initial} editing={editing} onSubmit={save} /></div></section>;
}
