import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { runApi, defaultOptions, validateOptions, type TestRun } from './api';

const clients: QueryClient[] = [];
const run: TestRun = { id: 'r1', projectId: 'p1', status: 'Pending', version: 'v1', createdAt: '2026-09-15T12:00:00Z', startedAt: null, finishedAt: null, runnerAvailable: false,
  configuration: { schemaVersion: 1, projectName: 'Portal', suiteName: 'Smoke', suiteVersion: 's1', environmentName: 'Staging', baseUrl: 'https://example.com/', environmentVersion: 'e1', cases: [{ id: 'c1', stableKey: 'login', name: 'Login', catalogVersion: 1, tags: [] }], tags: [], options: defaultOptions } };
function mount(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); clients.push(client);
  render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { clients.forEach(x => x.clear()); clients.length = 0; vi.restoreAllMocks(); });
it.each([{ workers: 0 }, { workers: 11 }, { workers: 1.5 }, { retries: -1 }, { retries: 6 }, { timeoutSeconds: 4 }, { timeoutSeconds: 301 }])('rejeita limites inválidos %s', change => {
  expect(validateOptions({ ...defaultOptions, ...change })).not.toBeNull();
});
it('valida os limites inclusivos', () => {
  expect(validateOptions({ ...defaultOptions, workers: 10, retries: 5, timeoutSeconds: 300 })).toBeNull();
});
it('exibe snapshot e preserva estado ao falhar cancelamento', async () => {
  vi.spyOn(runApi, 'get').mockResolvedValue(run);
  const cancel = vi.spyOn(runApi, 'cancel').mockRejectedValue(new Error('A execução mudou. Recarregue.'));
  mount('/test-runs/r1');
  await userEvent.click(await screen.findByRole('button', { name: 'Cancelar execução' }));
  expect(cancel).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Recarregue');
  expect(screen.getByRole('status')).toHaveTextContent('Pendente');
  expect(screen.getByText('Configuração salva')).toBeInTheDocument();
  expect(cancel).toHaveBeenCalledWith(run);
});
it('estado terminal não oferece cancelamento', async () => {
  vi.spyOn(runApi, 'get').mockResolvedValue({ ...run, status: 'Cancelled' });
  mount('/test-runs/r1');
  expect(await screen.findByText('Cancelada')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Cancelar execução' })).not.toBeInTheDocument();
});
