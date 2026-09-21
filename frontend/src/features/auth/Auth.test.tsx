import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthGate, authApi } from './Auth';
import { ApiError } from '../../api/http';
const clients: QueryClient[] = [];
afterEach(() => { clients.forEach(c => c.clear()); clients.length = 0; vi.restoreAllMocks(); });
function mount() { const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); clients.push(client); render(<QueryClientProvider client={client}><AuthGate><h1>Dados privados</h1></AuthGate></QueryClientProvider>); return client; }
it('protege conteúdo e limpa cache anterior ao entrar', async () => {
  const me = vi.spyOn(authApi, 'me').mockRejectedValue(new ApiError('Entre', 'AUTH_REQUIRED', 401));
  const login = vi.spyOn(authApi, 'login').mockImplementation(async () => { const user = { id: '1', login: 'operator', name: 'QA', role: 'Operator' as const, active: true, version: 'v1' }; me.mockResolvedValue(user); return user; });
  const client = mount(); client.setQueryData(['private-old'], 'sensitive');
  await screen.findByRole('heading', { name: 'Entrar no workspace' }); expect(screen.queryByText('Dados privados')).toBeNull();
  await userEvent.type(screen.getByLabelText('Login'), 'operator'); await userEvent.type(screen.getByLabelText('Senha'), 'a-valid-password');
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
  expect(await screen.findByText('Dados privados')).toBeInTheDocument(); expect(login).toHaveBeenCalledWith('operator', 'a-valid-password');
  expect(client.getQueryData(['private-old'])).toBeUndefined();
});
it('erro da API oferece recarga sem apresentar formulário como se a sessão fosse inválida', async () => {
  vi.spyOn(authApi, 'me').mockRejectedValue(new ApiError('Banco indisponível', 'DATABASE_UNAVAILABLE', 503));
  mount(); expect(await screen.findByRole('alert')).toHaveTextContent('Banco indisponível'); expect(screen.queryByLabelText('Senha')).toBeNull();
});
it('revogação remove dados privados e retorna ao login', async () => {
  vi.spyOn(authApi, 'me').mockResolvedValue({ id: '1', login: 'reader', name: 'QA', role: 'Reader', active: true, version: 'v1' });
  const client = mount(); await screen.findByText('Dados privados'); client.setQueryData(['private-old'], 'sensitive');
  window.dispatchEvent(new Event('qa:unauthorized'));
  await waitFor(() => expect(screen.queryByText('Dados privados')).toBeNull());
  expect(await screen.findByRole('heading', { name: 'Entrar no workspace' })).toBeInTheDocument(); expect(client.getQueryData(['private-old'])).toBeUndefined();
});
