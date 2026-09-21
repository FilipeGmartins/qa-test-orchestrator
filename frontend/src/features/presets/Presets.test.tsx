import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { PresetDetails } from './Presets';
import { presetApi, type Preset } from './api';
import { defaultOptions } from '../runs/api';
import { projectsApi } from '../projects/api';
const preset: Preset = { id: 'pr1', projectId: 'p1', name: 'Regression Full', description: 'Fluxo principal', revision: 2, version: 'v2', archived: false, createdAt: '2026-09-16T12:00:00Z', updatedAt: '2026-09-16T12:00:00Z',
  configuration: { testSuiteId: 's1', environmentId: 'e1', caseIds: ['c1'], tags: [], options: defaultOptions },
  snapshot: { schemaVersion: 1, projectName: 'Portal', suiteName: 'Smoke', suiteVersion: 'sv', environmentName: 'Staging', baseUrl: 'https://example.com', environmentVersion: 'ev', cases: [{ id: 'c1', stableKey: 'page-title', name: 'Title', catalogVersion: 1, tags: [] }], tags: [], options: defaultOptions } };
let client: QueryClient;
beforeEach(() => {
  vi.spyOn(presetApi, 'get').mockResolvedValue(preset);
  vi.spyOn(presetApi, 'revisions').mockResolvedValue({ items: [], total: 0 });
  vi.spyOn(projectsApi, 'get').mockResolvedValue({ id: 'p1', name: 'Portal', description: '', version: 'p', archivedAt: null, createdAt: '', updatedAt: '' });
});
afterEach(() => { client.clear(); vi.restoreAllMocks(); });
function mount() { client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/presets/pr1']}><Routes><Route path="/presets/:id" element={<PresetDetails />} /><Route path="/test-runs/:id" element={<p>Execução pendente criada</p>} /></Routes></MemoryRouter></QueryClientProvider>); }
it('exige revisão atual antes de criar uma execução pendente', async () => {
  const preview = { version: 'v2', fingerprint: 'hash', configuration: preset.snapshot, revision: 2, name: preset.name };
  vi.spyOn(presetApi, 'preview').mockResolvedValue(preview);
  const use = vi.spyOn(presetApi, 'use').mockResolvedValue({ id: 'r1', projectId: 'p1' } as never); mount();
  expect(screen.queryByRole('button', { name: 'Criar execução pendente' })).not.toBeInTheDocument();
  await userEvent.click(await screen.findByRole('button', { name: 'Revisar para usar' }));
  expect(use).not.toHaveBeenCalled();
  await userEvent.click(await screen.findByRole('button', { name: 'Criar execução pendente' }));
  expect(await screen.findByText('Execução pendente criada')).toBeInTheDocument(); expect(use).toHaveBeenCalledWith('pr1', preview);
});
it('mudança no catálogo bloqueia nova confirmação até revisar novamente', async () => {
  vi.spyOn(presetApi, 'preview').mockResolvedValue({ version: 'v2', fingerprint: 'hash', configuration: preset.snapshot, revision: 2, name: preset.name });
  vi.spyOn(presetApi, 'use').mockRejectedValue(new Error('O catálogo mudou.')); mount();
  await userEvent.click(await screen.findByRole('button', { name: 'Revisar para usar' }));
  await userEvent.click(await screen.findByRole('button', { name: 'Criar execução pendente' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('O catálogo mudou');
  expect(screen.getByRole('button', { name: 'Criar execução pendente' })).toBeDisabled();
  await userEvent.click(screen.getByRole('button', { name: 'Recarregar preset' }));
  expect(screen.queryByRole('button', { name: 'Criar execução pendente' })).not.toBeInTheDocument();
});
it('preset arquivado preserva consulta e não oferece ações de escrita', async () => {
  vi.mocked(presetApi.get).mockResolvedValue({ ...preset, archived: true }); mount();
  expect(await screen.findByText('Arquivado · revisão 2')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Revisar para usar' })).not.toBeInTheDocument(); expect(screen.queryByRole('link', { name: 'Editar preset' })).not.toBeInTheDocument();
});
