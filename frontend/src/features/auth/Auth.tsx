import { createContext, useContext, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiRequest, ApiError } from '../../api/http';
export interface User { id: string; login: string; name: string; role: 'Admin' | 'Operator' | 'Reader'; active: boolean; version: string }
export const roleNames = { Admin: 'Administrador', Operator: 'Operador', Reader: 'Leitor' };
const AuthContext = createContext<User | null>(null);
export const useCanWrite = () => useContext(AuthContext)?.role !== 'Reader';
export const useUser = () => useContext(AuthContext);
export const authApi = {
  me: () => apiRequest<User>('/auth/me'),
  login: (login: string, password: string) => apiRequest<User>('/auth/login', { method: 'POST', body: JSON.stringify({ login, password }) }),
  logout: () => apiRequest('/auth/logout', { method: 'POST' }),
};
export function AuthGate({ children }: { children: ReactNode }) {
  const client = useQueryClient(); const [expired, setExpired] = useState(false);
  const session = useQuery({ queryKey: ['session'], queryFn: authApi.me, enabled: !expired, retry: false, staleTime: 0, refetchInterval: 60000 });
  useEffect(() => {
    const clear = () => { void client.cancelQueries(); client.clear(); setExpired(true); };
    window.addEventListener('qa:unauthorized', clear); return () => window.removeEventListener('qa:unauthorized', clear);
  }, [client]);
  useEffect(() => { if (session.error instanceof ApiError && session.error.status === 401) { void client.cancelQueries(); client.clear(); setExpired(true); } }, [session.error, client]);
  if (session.isPending && !expired) return <main className="login-page"><p role="status">Verificando sessão…</p></main>;
  if (expired || session.error instanceof ApiError && session.error.status === 401) return <Login onLogin={user => { client.clear(); client.setQueryData(['session'], user); setExpired(false); }} />;
  if (session.isError) return <main className="login-page"><h1>Acesso ao workspace</h1><p role="alert">{session.error.message}</p><button className="button" onClick={() => void session.refetch()}>Tentar novamente</button></main>;
  if (!session.data) return null;
  return <AuthContext.Provider value={session.data}>{children}</AuthContext.Provider>;
}
function Login({ onLogin }: { onLogin: (user: User) => void }) {
  const [login, setLogin] = useState(''); const [password, setPassword] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('');
    try { onLogin(await authApi.login(login, password)); } catch (e) { setError((e as Error).message); } finally { setBusy(false); setPassword(''); }
  }
  return <main className="login-page"><p className="eyebrow">QA ORCHESTRATOR</p><h1>Entrar no workspace</h1><p>Use a conta fornecida pelo administrador.</p><form className="project-form" onSubmit={submit}>
    <div className="field"><label htmlFor="login">Login</label><input id="login" autoComplete="username" value={login} maxLength={80} required onChange={e => setLogin(e.target.value)} /></div>
    <div className="field"><label htmlFor="password">Senha</label><input id="password" type="password" autoComplete="current-password" value={password} maxLength={128} required onChange={e => setPassword(e.target.value)} /></div>
    {error && <p role="alert" className="error-message">{error}</p>}<button className="button primary" disabled={busy}>{busy ? 'Entrando…' : 'Entrar'}</button>
  </form></main>;
}
export function AccountPage() {
  const user = useUser(); const client = useQueryClient(); const [current, setCurrent] = useState(''); const [password, setPassword] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function logout() { setBusy(true); setError(''); try { await authApi.logout(); client.clear(); window.dispatchEvent(new Event('qa:unauthorized')); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  async function change(e: FormEvent) { e.preventDefault(); setBusy(true); setError(''); try { await apiRequest('/auth/password', { method: 'POST', body: JSON.stringify({ currentPassword: current, newPassword: password }) }); client.clear(); window.dispatchEvent(new Event('qa:unauthorized')); } catch (e) { setError((e as Error).message); } finally { setBusy(false); setCurrent(''); setPassword(''); } }
  return <><h1>Minha conta</h1><p>{user?.name} · {user && roleNames[user.role]}</p><button className="button" onClick={() => void logout()} disabled={busy}>Sair</button><form className="project-form" onSubmit={change}><h2>Alterar senha</h2><p>A troca encerra todas as suas sessões. Entre novamente com a nova senha.</p><div className="field"><label htmlFor="current-password">Senha atual</label><input id="current-password" type="password" autoComplete="current-password" required maxLength={128} value={current} onChange={e => setCurrent(e.target.value)} /></div><div className="field"><label htmlFor="new-password">Nova senha</label><input id="new-password" type="password" autoComplete="new-password" required minLength={12} maxLength={128} value={password} onChange={e => setPassword(e.target.value)} /></div>{error && <p role="alert">{error}</p>}<button className="button primary" disabled={busy}>Alterar senha e sair</button></form></>;
}
export function UsersPage() {
  const user = useUser(); const client = useQueryClient(); const [page, setPage] = useState(1); const [editing, setEditing] = useState<User | null>(null);
  const [login, setLogin] = useState(''); const [name, setName] = useState(''); const [role, setRole] = useState('Reader'); const [active, setActive] = useState(true); const [password, setPassword] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const query = useQuery({ queryKey: ['users', page], queryFn: () => apiRequest<{ items: User[]; total: number }>(`/users?page=${page}`), enabled: user?.role === 'Admin' });
  if (user?.role !== 'Admin') return <p role="alert">Somente administradores podem gerenciar usuários.</p>;
  function select(value: User | null) { setEditing(value); setLogin(value?.login ?? ''); setName(value?.name ?? ''); setRole(value?.role ?? 'Reader'); setActive(value?.active ?? true); setPassword(''); setError(''); }
  async function save(e: FormEvent) { e.preventDefault(); setBusy(true); setError(''); try { await apiRequest(`/users${editing ? '/' + editing.id : ''}`, { method: editing ? 'PUT' : 'POST', body: JSON.stringify({ login, name, role, active, version: editing?.version, password: password || undefined }) }); select(null); await client.invalidateQueries({ queryKey: ['users'] }); await client.invalidateQueries({ queryKey: ['session'] }); } catch (e) { setError((e as Error).message); } finally { setBusy(false); setPassword(''); } }
  return <><h1>Usuários e permissões</h1><p>Administrador gerencia usuários; Operador edita e executa; Leitor consulta resultados e evidências. Os perfis valem para todo o workspace.</p>
    {query.isError && <p role="alert">{query.error.message}<button className="button" onClick={() => void query.refetch()}>Recarregar usuários</button></p>}
    <div className="project-grid">{query.data?.items.map(u => <article className="project-card" key={u.id}><h2>{u.name}</h2><p>{u.login} · {roleNames[u.role]} · {u.active ? 'Ativo' : 'Desativado'}</p><button className="button" onClick={() => select(u)}>Editar {u.login}</button></article>)}</div>
    <div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button className="button" disabled={!query.data || page * 20 >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div>
    <form className="project-form" onSubmit={save}><h2>{editing ? 'Editar usuário' : 'Novo usuário'}</h2><fieldset className="catalog-fields" disabled={busy}>
    <div className="field"><label htmlFor="user-login">Login do usuário</label><input id="user-login" value={login} required minLength={3} maxLength={80} autoComplete="off" onChange={e => setLogin(e.target.value)} /></div>
    <div className="field"><label htmlFor="user-name">Nome</label><input id="user-name" value={name} required maxLength={120} onChange={e => setName(e.target.value)} /></div>
    <div className="field"><label htmlFor="user-role">Perfil</label><select id="user-role" value={role} onChange={e => setRole(e.target.value)}>{Object.entries(roleNames).map(([key,label]) => <option key={key} value={key}>{label}</option>)}</select></div>
    <label><input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} /> Usuário ativo</label>
    <div className="field"><label htmlFor="user-password">{editing ? 'Redefinir senha (opcional)' : 'Senha inicial'}</label><input id="user-password" type="password" required={!editing} minLength={12} maxLength={128} autoComplete="new-password" value={password} onChange={e => setPassword(e.target.value)} /></div>
    <p>Alterações revogam as sessões atuais desse usuário.</p>{error && <p role="alert">{error}</p>}<button className="button primary">{busy ? 'Salvando…' : 'Salvar usuário'}</button>{editing && <button type="button" className="button" onClick={() => select(null)}>Cancelar edição</button>}</fieldset></form>
  </>;
}
