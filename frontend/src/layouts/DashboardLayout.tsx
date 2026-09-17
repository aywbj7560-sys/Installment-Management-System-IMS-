import { useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Sidebar } from '../components/Sidebar';
export function DashboardLayout() { const { user, logout } = useAuth(); const [open, setOpen] = useState(false); const navigate = useNavigate(); if (!user) return null; const signOut = () => { logout(); navigate('/login', { replace: true }); }; return <div className="app-shell"><Sidebar role={user.role} open={open} close={() => setOpen(false)} /><div className="main"><header><button className="menu" aria-label="Open navigation" onClick={() => setOpen(true)}>☰</button><div className="user"><div><strong>{user.email}</strong><span>{user.role}</span></div><button className="secondary" onClick={signOut}>Log out</button></div></header><main><Outlet /></main></div></div>; }
