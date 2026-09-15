import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { projectsApi } from '../projects/api';
import { suiteApi } from '../suites/api';
import { catalogApi, validateEnvironment } from './api';

const project = { id: 'p1', name: 'Portal', description: '', archivedAt: null, version: 'p1', createdAt: '', updatedAt: '' };
const clients: QueryClient[] = [];
function mount(path: string) {
  vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); clients.push(client);
  render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { clients.forEach(x => x.clear()); clients.length = 0; vi.restoreAllMocks(); });

it.each(['file:///tmp/a', 'https://user:password@example.com', 'https://example.com?token=x', 'https://example.com#x'])('rejeita URL inadequada %s', baseUrl => {
  expect(validateEnvironment({ name: 'Staging', baseUrl, enabled: true })).not.toBeNull();
});
it('bloqueia Production e preserva URL em erro de gravação', async () => {
  vi.spyOn(catalogApi, 'environments').mockResolvedValue([]);
  const save = vi.spyOn(catalogApi, 'saveEnvironment').mockRejectedValue(new Error('Conflito. Recarregue.'));
  mount('/projects/p1/environments');
  await userEvent.click(await screen.findByRole('button', { name: '+ Novo ambiente' }));
  await userEvent.selectOptions(screen.getByLabelText('Ambiente'), 'Production');
  expect(screen.getByLabelText('Disponibilidade')).toBeDisabled();
  await userEvent.type(screen.getByLabelText('URL base'), 'https://example.com');
  await userEvent.click(screen.getByRole('button', { name: 'Salvar ambiente' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Conflito');
  expect(screen.getByLabelText('URL base')).toHaveValue('https://example.com');
  expect(save).toHaveBeenCalledWith('p1', { name: 'Production', baseUrl: 'https://example.com', enabled: false }, undefined);
});
it('valida chave antes de enviar e normaliza o cadastro do caso', async () => {
  vi.spyOn(suiteApi, 'get').mockResolvedValue({ id: 's1', projectId: 'p1', name: 'Smoke', description: '', tags: [], status: 'Active', version: 'v1', createdAt: '', updatedAt: '' });
  vi.spyOn(catalogApi, 'cases').mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 12 });
  const save = vi.spyOn(catalogApi, 'saveCase').mockRejectedValue(new Error('Falha temporária'));
  mount('/test-suites/s1/test-cases');
  await userEvent.click(await screen.findByRole('button', { name: '+ Novo caso' }));
  await userEvent.type(screen.getByLabelText('Nome do caso'), 'Login');
  await userEvent.type(screen.getByLabelText('Chave permanente'), '../login');
  await userEvent.click(screen.getByRole('button', { name: 'Salvar caso' }));
  expect(save).not.toHaveBeenCalled();
  await userEvent.clear(screen.getByLabelText('Chave permanente'));
  await userEvent.type(screen.getByLabelText('Chave permanente'), 'LOGIN');
  await userEvent.type(screen.getByLabelText('Tags do caso'), '@SMOKE');
  await userEvent.click(screen.getByRole('button', { name: 'Salvar caso' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Falha temporária');
  expect(save).toHaveBeenCalledWith('s1', { stableKey: 'login', name: 'Login', description: '', tags: ['@smoke'], status: 'Active' }, undefined);
});
