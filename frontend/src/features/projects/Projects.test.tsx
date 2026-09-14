import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { ApiError, projectsApi, validateProject, type Project } from './api';

const project: Project = { id: '11111111-1111-1111-1111-111111111111', name: 'Portal cliente', description: 'Área do cliente', version: '22222222-2222-2222-2222-222222222222', createdAt: '2026-09-14T12:00:00Z', updatedAt: '2026-09-14T12:00:00Z', archivedAt: null };
const clients: QueryClient[] = [];
function mount(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  clients.push(client);
  render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.restoreAllMocks(); });

describe('Projetos', () => {
  it('valida campos obrigatórios e limites', () => {
    expect(validateProject({ name: '   ', description: '' }).name).toBeDefined();
    expect(validateProject({ name: 'a'.repeat(121), description: 'b'.repeat(2001) })).toHaveProperty('description');
    expect(validateProject({ name: 'a'.repeat(120), description: 'b'.repeat(2000) })).toEqual({});
  });
  it('bloqueia envio sem nome', async () => {
    const create = vi.spyOn(projectsApi, 'create');
    mount('/projects/new');
    await userEvent.click(screen.getByRole('button', { name: 'Criar projeto' }));
    expect(screen.getByText('Informe um nome com 1 a 120 caracteres.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });
  it('cria e abre o detalhe com os dados salvos', async () => {
    const create = vi.spyOn(projectsApi, 'create').mockResolvedValue(project);
    vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
    mount('/projects/new');
    await userEvent.type(screen.getByLabelText(/Nome do projeto/), ' Portal cliente ');
    await userEvent.type(screen.getByLabelText(/Descrição/), 'Área do cliente');
    await userEvent.click(screen.getByRole('button', { name: 'Criar projeto' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Portal cliente' })).toBeInTheDocument();
    expect(create).toHaveBeenCalledWith({ name: 'Portal cliente', description: 'Área do cliente' });
  });
  it('preserva preenchimento após erro do banco', async () => {
    vi.spyOn(projectsApi, 'create').mockRejectedValue(new ApiError('Banco indisponível.', 'DATABASE_UNAVAILABLE', 503));
    mount('/projects/new');
    await userEvent.type(screen.getByLabelText(/Nome do projeto/), 'Portal');
    await userEvent.click(screen.getByRole('button', { name: 'Criar projeto' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Banco indisponível.');
    expect(screen.getByLabelText(/Nome do projeto/)).toHaveValue('Portal');
  });
  it('exibe loading e erro na listagem com nova tentativa', async () => {
    const list = vi.spyOn(projectsApi, 'list').mockRejectedValue(new Error('Serviço indisponível'));
    mount('/projects');
    expect(screen.getByRole('status')).toHaveTextContent('Carregando projetos');
    expect(await screen.findByRole('alert')).toHaveTextContent('Serviço indisponível');
    list.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 12 });
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(await screen.findByText('Seu primeiro projeto começa aqui')).toBeInTheDocument();
  });
  it('envia busca e filtro com página reiniciada', async () => {
    const list = vi.spyOn(projectsApi, 'list').mockResolvedValue({ items: [project], total: 25, page: 2, pageSize: 12 });
    mount('/projects?page=2');
    await screen.findByRole('link', { name: 'Abrir projeto Portal cliente' });
    await userEvent.selectOptions(screen.getByLabelText('Status'), 'archived');
    await waitFor(() => expect(list).toHaveBeenLastCalledWith('', 'archived', 1, expect.any(AbortSignal)));
    await userEvent.type(screen.getByLabelText('Buscar por nome'), 'Portal');
    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    await waitFor(() => expect(list).toHaveBeenLastCalledWith('Portal', 'archived', 1, expect.any(AbortSignal)));
  });
  it('envia a versão na edição e apresenta conflito sem perder o formulário', async () => {
    vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
    const update = vi.spyOn(projectsApi, 'update').mockRejectedValue(new ApiError('Recarregue os dados.', 'CONCURRENT_UPDATE', 409));
    mount(`/projects/${project.id}/edit`);
    await screen.findByDisplayValue('Portal cliente');
    await userEvent.clear(screen.getByLabelText(/Nome do projeto/));
    await userEvent.type(screen.getByLabelText(/Nome do projeto/), 'Portal atualizado');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Recarregue os dados.');
    expect(update).toHaveBeenCalledWith(project.id, { name: 'Portal atualizado', description: project.description }, project.version);
    expect(screen.getByLabelText(/Nome do projeto/)).toHaveValue('Portal atualizado');
    expect(screen.getByRole('button', { name: 'Descartar edição e carregar versão atual' })).toBeInTheDocument();
  });
  it('só arquiva após confirmar e mantém o detalhe consultável', async () => {
    vi.spyOn(projectsApi, 'get').mockResolvedValue(project);
    const archive = vi.spyOn(projectsApi, 'archive').mockResolvedValue({ ...project, archivedAt: '2026-09-14T13:00:00Z' });
    mount(`/projects/${project.id}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Arquivar projeto' }));
    expect(archive).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar arquivamento' }));
    expect(await screen.findByText('Projeto arquivado. As informações foram preservadas.')).toBeInTheDocument();
    expect(archive).toHaveBeenCalledWith(project.id, project.version);
    expect(screen.queryByRole('link', { name: 'Editar projeto' })).not.toBeInTheDocument();
  });
});
