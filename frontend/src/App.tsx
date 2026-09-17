import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { DashboardLayout } from './layouts/DashboardLayout';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { AccessDeniedPage, ComingSoonPage, NotFoundPage } from './pages/StatusPages';
import { ProtectedRoute } from './routes/ProtectedRoute';
const modules = ['customers', 'products', 'contracts', 'payments', 'guarantors', 'installments', 'collections', 'reports', 'users'];
export function App() { return <BrowserRouter><AuthProvider><Routes><Route path="/login" element={<LoginPage />} /><Route element={<ProtectedRoute />}><Route element={<DashboardLayout />}><Route path="/dashboard" element={<DashboardPage />} />{modules.map(path => <Route key={path} path={`/${path}`} element={<ComingSoonPage />} />)}<Route path="/access-denied" element={<AccessDeniedPage />} /></Route></Route><Route path="/" element={<Navigate to="/dashboard" replace />} /><Route path="*" element={<NotFoundPage />} /></Routes></AuthProvider></BrowserRouter>; }
