import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { DashboardLayout } from './layouts/DashboardLayout';
import { DashboardPage } from './pages/DashboardPage';
import { CustomersPage } from './pages/CustomersPage';
import { CustomerDetailPage } from './pages/CustomerDetailPage';
import { CustomerFormPage } from './pages/CustomerFormPage';
import { ProductsPage } from './pages/ProductsPage';
import { ProductDetailPage } from './pages/ProductDetailPage';
import { ProductFormPage } from './pages/ProductFormPage';
import { ContractsPage } from './pages/ContractsPage';
import { ContractDetailPage } from './pages/ContractDetailPage';
import { ContractFormPage } from './pages/ContractFormPage';
import { LoginPage } from './pages/LoginPage';
import { AccessDeniedPage, ComingSoonPage, NotFoundPage } from './pages/StatusPages';
import { ProtectedRoute } from './routes/ProtectedRoute';
const modules = ['payments', 'guarantors', 'installments', 'collections', 'reports', 'users'];
export function App() { return <BrowserRouter><AuthProvider><Routes><Route path="/login" element={<LoginPage />} /><Route element={<ProtectedRoute />}><Route element={<DashboardLayout />}><Route path="/dashboard" element={<DashboardPage />} /><Route path="/customers" element={<CustomersPage />} /><Route path="/customers/new" element={<CustomerFormPage />} /><Route path="/customers/:id" element={<CustomerDetailPage />} /><Route path="/customers/:id/edit" element={<CustomerFormPage editing />} /><Route path="/products" element={<ProductsPage />} /><Route path="/products/new" element={<ProductFormPage />} /><Route path="/products/:id" element={<ProductDetailPage />} /><Route path="/products/:id/edit" element={<ProductFormPage editing />} /><Route path="/contracts" element={<ContractsPage />} /><Route path="/contracts/new" element={<ContractFormPage />} /><Route path="/contracts/:id" element={<ContractDetailPage />} />{modules.map(path => <Route key={path} path={`/${path}`} element={<ComingSoonPage />} />)}<Route path="/access-denied" element={<AccessDeniedPage />} /></Route></Route><Route path="/" element={<Navigate to="/dashboard" replace />} /><Route path="*" element={<NotFoundPage />} /></Routes></AuthProvider></BrowserRouter>; }
