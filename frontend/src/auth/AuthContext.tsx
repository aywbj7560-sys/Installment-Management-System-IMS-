import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { currentUserRequest, loginRequest } from '../api/auth';
import { setUnauthorizedHandler } from '../api/client';
import type { CurrentUser } from '../types/api';
import { tokenStorage } from './tokenStorage';
interface AuthValue { user: CurrentUser | null; token: string | null; loading: boolean; login(email: string, password: string): Promise<void>; logout(): void }
const AuthContext = createContext<AuthValue | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => tokenStorage.get());
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);
  const logout = useCallback(() => { tokenStorage.clear(); setToken(null); setUser(null); }, []);
  useEffect(() => { setUnauthorizedHandler(logout); return () => setUnauthorizedHandler(); }, [logout]);
  useEffect(() => { let active = true; if (!token) { setLoading(false); return; } setLoading(true); currentUserRequest(token).then(value => { if (active) setUser(value); }).catch(() => { if (active) logout(); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; }; }, [token, logout]);
  const login = useCallback(async (email: string, password: string) => { const result = await loginRequest({ email, password }); tokenStorage.set(result.token); setToken(result.token); setUser(result.user); }, []);
  const value = useMemo(() => ({ user, token, loading, login, logout }), [user, token, loading, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
export function useAuth() { const value = useContext(AuthContext); if (!value) throw new Error('useAuth must be used within AuthProvider'); return value; }
