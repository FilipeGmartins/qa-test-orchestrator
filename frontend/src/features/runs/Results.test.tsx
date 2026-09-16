import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, expect, it, vi } from 'vitest';
import { ResultsPanel, resultsApi, type AttemptResult } from './Results';
let client: QueryClient;
const item: AttemptResult = { id: 'a1', runId: 'r1', caseId: 'c1', caseName: 'Página inicial', stableKey: 'page-title', browser: 'Chromium', attempt: 1, status: 'failed', durationMs: 1250,
  error: 'Expected title', stack: 'at [PATH]', logs: 'Request failed', recordedAt: '2026-09-16T12:00:00Z', artifacts: [
    { id: 'e1', kind: 'screenshot', size: 2048, available: true, expiresAt: '2026-09-30T12:00:00Z' },
    { id: 'e2', kind: 'trace', size: 1024, available: false, expiresAt: '2026-09-10T12:00:00Z' }] };
function mount() {
  client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  render(<QueryClientProvider client={client}><MemoryRouter><ResultsPanel id="r1" /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { client.clear(); vi.restoreAllMocks(); });
it('mostra tentativas, falhas, downloads e expiração; filtra resultados', async () => {
  const list = vi.spyOn(resultsApi, 'list').mockResolvedValue({ items: [item], total: 1 }); mount();
  expect(await screen.findByText('Expected title')).toBeInTheDocument();
  expect(screen.getByText(/tentativa 2/)).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Baixar Screenshot' })).toHaveAttribute('href', '/api/test-runs/r1/artifacts/e1');
  expect(screen.getByText('Trace indisponível ou expirado')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Histórico deste caso' })).toHaveAttribute('href', '/test-cases/c1/results');
  await userEvent.click(screen.getByText('Stack trace')); expect(screen.getByText('at [PATH]')).toBeVisible();
  list.mockResolvedValue({ items: [], total: 0 });
  await userEvent.selectOptions(screen.getByLabelText('Resultado da tentativa'), 'passed');
  expect(await screen.findByText(/Nenhuma tentativa registrada/)).toBeInTheDocument();
  expect(list).toHaveBeenLastCalledWith('test-runs', 'r1', 'passed', 'all', 1, expect.any(AbortSignal));
});
it('permite recarregar após falha sem apresentar resultados inventados', async () => {
  const list = vi.spyOn(resultsApi, 'list').mockRejectedValueOnce(new Error('Banco indisponível')).mockResolvedValue({ items: [], total: 0 }); mount();
  expect(await screen.findByRole('alert')).toHaveTextContent('Banco indisponível');
  await userEvent.click(screen.getByRole('button', { name: 'Recarregar resultados' }));
  expect(await screen.findByText('0 tentativas encontradas')).toBeInTheDocument();
  expect(list).toHaveBeenCalledTimes(2);
});
