import { render, screen, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { Dashboard } from './Dashboard';
import { dashboardApi, type DashboardData } from './api';
import { projectsApi } from '../projects/api';
vi.mock('recharts', async importOriginal => ({ ...await importOriginal<typeof import('recharts')>(), ResponsiveContainer: () => null }));
const data: DashboardData = { from: '2026-09-01', toExclusive: '2026-09-17', generatedAt: '2026-09-16T12:00:00Z', totalRuns: 4,
  runCounts: { Pending: 1, Queued: 0, Running: 0, Passed: 1, Failed: 1, Error: 1, Cancelled: 0 }, completedRunsWithoutDetails: 1,
  passed: 3, failed: 1, skipped: 2, interrupted: 0, successRate: 75, averageTestDurationMs: 1200, flakyTests: 1,
  daily: [{ date: '2026-09-16', runs: 4, passed: 3, failed: 1, skipped: 2 }],
  recentRuns: [{ id: 'r1', projectName: 'Portal', suiteName: 'Smoke', status: 'Failed', createdAt: '2026-09-16T12:00:00Z' }],
  recentFailures: [{ runId: 'r1', caseId: 'c1', caseName: 'Title', browser: 'Chromium', error: 'Expected title', recordedAt: '2026-09-16T12:00:00Z' }],
  flakyCases: [{ caseId: 'c2', caseName: 'HTTP', browser: 'Chromium', recoveredRuns: 1 }] };
let client: QueryClient;
beforeEach(() => { vi.spyOn(projectsApi, 'list').mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 12 }); });
afterEach(() => { client?.clear(); vi.restoreAllMocks(); });
function mount() { client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); render(<QueryClientProvider client={client}><MemoryRouter><Dashboard /></MemoryRouter></QueryClientProvider>); }
it('mostra métricas reais, ressalva de cobertura e links para investigar', async () => {
  vi.spyOn(dashboardApi, 'get').mockResolvedValue(data); mount();
  expect(await screen.findByText('75%')).toBeInTheDocument();
  expect(screen.getByText(/concluídas sem detalhes/)).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Portal / Smoke' })).toHaveAttribute('href', '/test-runs/r1');
  expect(screen.getByRole('link', { name: 'HTTP · Chromium' })).toHaveAttribute('href', '/test-cases/c2/results');
  await userEvent.click(screen.getByText('Consultar valores diários em texto'));
  expect(screen.getByText(/2026-09-16: 4 execuções/)).toBeVisible();
});
it('aplica datas somente ao confirmar e rejeita intervalos inválidos', async () => {
  const get = vi.spyOn(dashboardApi, 'get').mockResolvedValue(data); mount(); await screen.findByText('75%');
  fireEvent.change(screen.getByLabelText('De (UTC)'), { target: { value: '2026-09-02' } });
  fireEvent.change(screen.getByLabelText('Até (UTC)'), { target: { value: '2026-09-01' } });
  await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Selecione de 1 a 90 dias'); expect(get).toHaveBeenCalledTimes(1);
  fireEvent.change(screen.getByLabelText('De (UTC)'), { target: { value: '2026-09-01' } });
  await userEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }));
  expect(get).toHaveBeenLastCalledWith('', '2026-09-01', '2026-09-01', expect.any(AbortSignal));
});
it('não apresenta zero como resposta de indisponibilidade e permite atualizar', async () => {
  vi.spyOn(dashboardApi, 'get').mockRejectedValueOnce(new Error('Banco indisponível')).mockResolvedValue(data); mount();
  expect(await screen.findByRole('alert')).toHaveTextContent('Banco indisponível'); expect(screen.queryByText('Taxa de sucesso')).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'Atualizar métricas' })); expect(await screen.findByText('75%')).toBeInTheDocument();
});
it('explica ausência de execuções e não inventa duração', async () => {
  vi.spyOn(dashboardApi, 'get').mockResolvedValue({ ...data, totalRuns: 0, averageTestDurationMs: null, recentRuns: [], recentFailures: [], flakyCases: [] }); mount();
  expect(await screen.findByText(/Nenhuma execução neste período/)).toBeInTheDocument();
  expect(within(screen.getByText('Duração média por teste').closest('article')!).getByText('Sem avaliações')).toBeInTheDocument();
});
