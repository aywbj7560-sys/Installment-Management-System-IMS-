import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getProducts } from '../api/products';
import { useAuth } from '../auth/AuthContext';
import { ActiveStatusBadge } from '../components/ActiveStatusBadge';
import { Pagination } from '../components/Pagination';
import type { ProductPage } from '../types/products';
import { formatMoney } from '../utils/format';
import { canManageProducts } from '../utils/roles';

type ActiveFilter = '' | 'true' | 'false';
export function ProductsPage() {
  const { token, user } = useAuth();
  const [search, setSearch] = useState(''); const [query, setQuery] = useState('');
  const [active, setActive] = useState<ActiveFilter>(''); const [page, setPage] = useState(1);
  const [data, setData] = useState<ProductPage>(); const [loading, setLoading] = useState(true);
  const [error, setError] = useState(''); const [retry, setRetry] = useState(0);
  useEffect(() => { const timer = setTimeout(() => { setQuery(search.trim()); setPage(1); }, 350); return () => clearTimeout(timer); }, [search]);
  useEffect(() => {
    if (!token) return; let current = true; setLoading(true); setError('');
    getProducts({ search: query || undefined, isActive: active ? active === 'true' : undefined, page, pageSize: 10 }, token)
      .then(result => { if (current) setData(result); })
      .catch(() => { if (current) setError('Products could not be loaded. Please try again.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [token, query, active, page, retry]);
  const reset = () => { setSearch(''); setQuery(''); setActive(''); setPage(1); };
  return <section><div className="page-title"><div><p className="eyebrow">Product management</p><h1>Products</h1><p>View product pricing and availability.</p></div>{user && canManageProducts(user.role) && <Link className="button-link primary" to="/products/new">Create product</Link>}</div>
    <div className="panel filters"><label>Search<input aria-label="Search products" value={search} maxLength={150} placeholder="Code, name, description…" onChange={e => setSearch(e.target.value)} /></label><label>Availability<select aria-label="Product availability" value={active} onChange={e => { setActive(e.target.value as ActiveFilter); setPage(1); }}><option value="">All products</option><option value="true">Active</option><option value="false">Inactive</option></select></label><button className="secondary" onClick={reset}>Clear filters</button></div>
    {loading ? <div className="inline-state">Loading products…</div> : error ? <div className="panel error-state" role="alert"><h2>Unable to load products</h2><p>{error}</p><button className="secondary" onClick={() => setRetry(value => value + 1)}>Try again</button></div> : !data?.items.length ? <div className="panel empty-state"><h2>No products found</h2><p>Try changing the search or availability filter.</p></div> : <div className="panel table-panel"><div className="table-scroll"><table><thead><tr><th>ID</th><th>Product code</th><th>Name</th><th>Cash price</th><th>Installment price</th><th>Status</th><th>Actions</th></tr></thead><tbody>{data.items.map(product => <tr key={product.productId}><td>{product.productId}</td><td><Link to={`/products/${product.productId}`}>{product.productCode}</Link></td><td>{product.name}</td><td className="numeric">{formatMoney(product.cashPrice)}</td><td className="numeric">{product.installmentPrice === null ? '—' : formatMoney(product.installmentPrice)}</td><td><ActiveStatusBadge isActive={product.isActive} /></td><td className="actions"><Link to={`/products/${product.productId}`}>View</Link>{user && canManageProducts(user.role) && <Link to={`/products/${product.productId}/edit`}>Edit</Link>}</td></tr>)}</tbody></table></div><Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPage={setPage} /></div>}
  </section>;
}
