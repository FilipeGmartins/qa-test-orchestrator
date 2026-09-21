import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { PageTests, pageTestApi } from './PageTests';
import { projectsApi } from '../projects/api';
import { catalogApi } from '../catalog/api';
import { runApi, type TestRun } from '../runs/api';
const clients: QueryClient[] = [];
beforeEach(() => {
  vi.spyOn(projectsApi, 'list').mockResolvedValue({ items: [{ id: 'p1', name: 'Portal', archivedAt: null } as never], total: 1, page: 1, pageSize: 12 });
  vi.spyOn(catalogApi, 'environments').mockResolvedValue([{ id: 'e1', name: 'Staging', baseUrl: 'https://staging.example', enabled: true } as never]);
  vi.spyOn(runApi, 'capabilities').mockResolvedValue({ enabled: true, catalog: [] });
});
afterEach(() => { clients.forEach(c => c.clear()); clients.length = 0; vi.restoreAllMocks(); });
function mount() { const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); clients.push(client); render(<QueryClientProvider client={client}><MemoryRouter><Routes><Route path="/" element={<PageTests />} /><Route path="/test-runs/:id" element={<h1>Execução salva</h1>} /></Routes></MemoryRouter></QueryClientProvider>); }
async function configure(url = 'https://staging.example/catalog') { await screen.findByRole('option', { name: 'Portal' }); await userEvent.selectOptions(screen.getByLabelText('Projeto'), 'p1'); await screen.findByRole('option', { name: /Staging/ }); await userEvent.selectOptions(screen.getByLabelText('Ambiente'), 'e1'); await userEvent.type(screen.getByLabelText('URL da página'), url); }
it('exige revisão e salva seleção sem iniciar execução', async () => {
  const create = vi.spyOn(pageTestApi, 'create').mockResolvedValue({ id: 'r1' } as TestRun); const enqueue = vi.spyOn(runApi, 'enqueue');
  mount(); await configure(); await userEvent.click(screen.getByRole('button', { name: 'Revisar configuração' }));
  expect(await screen.findByText(/9 verificações/)).toBeInTheDocument(); expect(create).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: 'Salvar teste como pendente' })); await screen.findByText('Execução salva');
  expect(create).toHaveBeenCalledWith('p1', { environmentId: 'e1', url: 'https://staging.example/catalog', checks: ['load','console','layout'], devices: ['desktop','tablet','mobile'] }); expect(enqueue).not.toHaveBeenCalled();
});
it('bloqueia origem diferente antes de enviar', async () => { const create = vi.spyOn(pageTestApi, 'create'); mount(); await configure('https://unapproved.example/'); await userEvent.click(screen.getByRole('button', { name: 'Revisar configuração' })); expect(await screen.findByRole('alert')).toHaveTextContent('mesma origem'); expect(create).not.toHaveBeenCalled(); });
it('preserva configuração ao receber rejeição do servidor', async () => { vi.spyOn(pageTestApi, 'create').mockRejectedValue(new Error('Origem não aprovada')); mount(); await configure(); await userEvent.click(screen.getByRole('button', { name: 'Revisar configuração' })); await userEvent.click(screen.getByRole('button', { name: 'Salvar teste como pendente' })); expect(await screen.findByRole('alert')).toHaveTextContent('Origem não aprovada'); await userEvent.click(screen.getByRole('button', { name: 'Voltar e editar' })); expect(screen.getByLabelText('URL da página')).toHaveValue('https://staging.example/catalog'); });
