import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { projectsApi, type Project } from '../projects/api';
import { suiteApi, parseTags, validateSuite, type Suite } from './api';
import { ApiError } from '../../api/http';

const project: Project = { id: 'p1', name: 'Portal', description: '', version: 'v1', createdAt: '2026-09-14T12:00:00Z', updatedAt: '2026-09-14T12:00:00Z', archivedAt: null };
const suite: Suite = { id: 's1', projectId: 'p1', name: 'Smoke', description: '', tags: ['@smoke'], status: 'Active', version: 's-v1', createdAt: project.createdAt, updatedAt: project.updatedAt };
const clients: QueryClient[] = [];
function mount(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  clients.push(client);
  render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.restoreAllMocks(); });

it('valida e normaliza tags', () => {
  expect(parseTags('@SMOKE, @login')).toEqual(['@smoke', '@login']);
  expect(validateSuite({ name: 'Smoke', description: '', tags: ['bad'], status: 'Active' })).toContain('Use tags');
  expect(validateSuite({ name: 'Smoke', description: '', tags: Array(21).fill('@tag'), status: 'Active' })).toContain('20 tags');
});
it('valida nome antes de criar e envia configuração estruturada', async () => {
  vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
  const create = vi.spyOn(suiteApi, 'create').mockResolvedValue(suite);
  vi.spyOn(suiteApi, 'list').mockResolvedValue({ items: [suite], total: 1, page: 1, pageSize: 12 });
  mount('/projects/p1/test-suites/new');
  await userEvent.click(await screen.findByRole('button', { name: 'Criar suíte' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Informe um nome');
  expect(create).not.toHaveBeenCalled();
  await userEvent.type(screen.getByLabelText('Nome da suíte'), 'Smoke');
  await userEvent.type(screen.getByLabelText('Tags'), '@SMOKE @login');
  await userEvent.click(screen.getByRole('button', { name: 'Criar suíte' }));
  expect(await screen.findByRole('link', { name: 'Abrir suíte Smoke' })).toBeInTheDocument();
  expect(create).toHaveBeenCalledWith('p1', { name: 'Smoke', description: '', tags: ['@smoke', '@login'], status: 'Active' });
});
it('projeto arquivado torna a suíte somente leitura', async () => {
  vi.spyOn(projectsApi, 'get').mockResolvedValue({ ...project, archivedAt: project.updatedAt });
  vi.spyOn(suiteApi, 'get').mockResolvedValue(suite);
  mount('/test-suites/s1');
  expect(await screen.findByLabelText('Nome da suíte')).toBeDisabled();
  expect(screen.queryByRole('button', { name: 'Salvar suíte' })).not.toBeInTheDocument();
});
it('envia a suíte atual ao inativar e preserva edição em conflito', async () => {
  vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
  vi.spyOn(suiteApi, 'get').mockResolvedValue(suite);
  const update = vi.spyOn(suiteApi, 'update').mockRejectedValue(new ApiError('Conflito de edição.', 'CONCURRENT_UPDATE', 409));
  mount('/test-suites/s1');
  await userEvent.selectOptions(await screen.findByLabelText('Status da suíte'), 'Inactive');
  await userEvent.click(screen.getByRole('button', { name: 'Salvar suíte' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Conflito');
  expect(update).toHaveBeenCalledWith(suite, { name: 'Smoke', description: '', tags: ['@smoke'], status: 'Inactive' });
  expect(screen.getByLabelText('Status da suíte')).toHaveValue('Inactive');
});
it('mostra falha e permite recarregar a listagem', async () => {
  vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
  const list = vi.spyOn(suiteApi, 'list').mockRejectedValue(new Error('Banco indisponível'));
  mount('/projects/p1/test-suites');
  expect(await screen.findByRole('alert')).toHaveTextContent('Banco indisponível');
  list.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 12 });
  await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
  expect(await screen.findByText('Nenhuma suíte neste filtro')).toBeInTheDocument();
});
