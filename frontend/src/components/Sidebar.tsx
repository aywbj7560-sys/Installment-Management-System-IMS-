import { NavLink } from 'react-router-dom';
import type { Role } from '../types/api';
type Item = { label: string; path: string; roles?: Role[] };
const financial: Role[] = ['Admin', 'Financial Manager', 'Auditor'];
const collection: Role[] = [...financial, 'Collection Officer'];
const items: Item[] = [
  { label: 'Dashboard', path: '/dashboard' }, { label: 'Customers', path: '/customers' }, { label: 'Products', path: '/products' },
  { label: 'Contracts', path: '/contracts' }, { label: 'Payments', path: '/payments', roles: collection }, { label: 'Guarantors', path: '/guarantors' },
  { label: 'Installments', path: '/installments' }, { label: 'Collections', path: '/collections', roles: collection }, { label: 'Reports', path: '/reports', roles: collection },
  { label: 'Users', path: '/users', roles: ['Admin'] },
];
export function Sidebar({ role, open, close }: { role: Role; open: boolean; close(): void }) { return <><button aria-label="Close navigation" className={`backdrop ${open ? 'show' : ''}`} onClick={close} /><aside className={`sidebar ${open ? 'open' : ''}`}><div className="brand"><span>IMS</span><small>Installment Management</small></div><nav aria-label="Main navigation">{items.filter(i => !i.roles || i.roles.includes(role)).map(i => <NavLink key={i.path} to={i.path} onClick={close}>{i.label}</NavLink>)}</nav></aside></>; }
