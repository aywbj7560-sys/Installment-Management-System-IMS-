import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getContracts } from '../api/contracts';
import { useAuth } from '../auth/AuthContext';
import { ContractStatusBadge } from '../components/ContractStatusBadge';
import { Pagination } from '../components/Pagination';
import { contractStatuses, type ContractPage, type ContractStatus } from '../types/contracts';
import { formatMoney } from '../utils/format';
import { canCreateContracts } from '../utils/roles';

export function ContractsPage() {
  const { token, user } = useAuth(); const [search, setSearch] = useState(''); const [query, setQuery] = useState(''); const [status, setStatus] = useState<ContractStatus | ''>(''); const [page, setPage] = useState(1); const [data, setData] = useState<ContractPage>(); const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [retry, setRetry] = useState(0);
  useEffect(() => { const timer = setTimeout(() => { setQuery(search.trim()); setPage(1); }, 350); return () => clearTimeout(timer); }, [search]);
  useEffect(() => { if (!token) return; let current = true; setLoading(true); setError(''); getContracts({ search: query || undefined, status: status || undefined, page, pageSize: 10 }, token).then(result => { if (current) setData(result); }).catch(() => { if (current) setError('Contracts could not be loaded. Please try again.'); }).finally(() => { if (current) setLoading(false); }); return () => { current = false; }; }, [token, query, status, page, retry]);
  const reset = () => { setSearch(''); setQuery(''); setStatus(''); setPage(1); };
  return <section><div className="page-title"><div><p className="eyebrow">Contract management</p><h1>Contracts</h1><p>View contract terms, financing, and current lifecycle status.</p></div>{user && canCreateContracts(user.role) && <Link className="button-link primary" to="/contracts/new">Create contract</Link>}</div>
    <div className="panel filters"><label>Search<input aria-label="Search contracts" value={search} maxLength={150} placeholder="Contract number or customer…" onChange={e => setSearch(e.target.value)} /></label><label>Status<select aria-label="Contract status" value={status} onChange={e => { setStatus(e.target.value as ContractStatus | ''); setPage(1); }}><option value="">All statuses</option>{contractStatuses.map(value => <option key={value}>{value}</option>)}</select></label><button className="secondary" onClick={reset}>Clear filters</button></div>
    {loading ? <div className="inline-state">Loading contracts…</div> : error ? <div className="panel error-state" role="alert"><h2>Unable to load contracts</h2><p>{error}</p><button className="secondary" onClick={() => setRetry(value => value + 1)}>Try again</button></div> : !data?.items.length ? <div className="panel empty-state"><h2>No contracts found</h2><p>Try changing the search or status filter.</p></div> : <div className="panel table-panel"><div className="table-scroll"><table><thead><tr><th>ID</th><th>Contract number</th><th>Customer</th><th>Contract date</th><th>Total</th><th>Down payment</th><th>Remaining</th><th>Status</th><th>Action</th></tr></thead><tbody>{data.items.map(contract => <tr key={contract.contractId}><td>{contract.contractId}</td><td><Link to={`/contracts/${contract.contractId}`}>{contract.contractNumber}</Link></td><td>{contract.customerName}</td><td>{new Date(contract.contractDate).toLocaleDateString()}</td><td className="numeric">{formatMoney(contract.totalAmount)}</td><td className="numeric">{formatMoney(contract.downPayment)}</td><td className="numeric">{formatMoney(contract.remainingAmount)}</td><td><ContractStatusBadge status={contract.status} /></td><td><Link to={`/contracts/${contract.contractId}`}>View</Link></td></tr>)}</tbody></table></div><Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPage={setPage} /></div>}
  </section>;
}
