import { Link, useLocation } from 'react-router-dom';
export function ComingSoonPage() { const name = useLocation().pathname.slice(1); return <div className="empty"><span>Next stage</span><h1>{name.charAt(0).toUpperCase() + name.slice(1)}</h1><p>This module is not included in the current frontend stage.</p><Link to="/dashboard">Return to dashboard</Link></div>; }
export function AccessDeniedPage() { return <div className="empty"><span>403</span><h1>Access denied</h1><p>You do not have permission to view this area.</p><Link to="/dashboard">Return to dashboard</Link></div>; }
export function NotFoundPage() { return <div className="empty"><span>404</span><h1>Page not found</h1><p>The page you requested does not exist.</p><Link to="/dashboard">Return to dashboard</Link></div>; }
