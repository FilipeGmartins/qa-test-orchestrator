import { apiRequest } from '../../api/http';
export const statuses = ['Pending', 'Queued', 'Running', 'Passed', 'Failed', 'Error', 'Cancelled'] as const;
export type RunStatus = typeof statuses[number];
export const statusLabels: Record<RunStatus, string> = { Pending: 'Pendente', Queued: 'Na fila', Running: 'Em execução', Passed: 'Aprovada', Failed: 'Reprovada', Error: 'Erro de infraestrutura', Cancelled: 'Cancelada' };
export interface RunOptions {
  testType: string; browser: string; mode: string; workers: number; retries: number; timeoutSeconds: number;
  screenshot: string; video: string; trace: string;
}
export const defaultOptions: RunOptions = { testType: 'Smoke', browser: 'Chromium', mode: 'Headless', workers: 1, retries: 0, timeoutSeconds: 60, screenshot: 'OnFailure', video: 'OnFailure', trace: 'OnFailure' };
export interface RunInput { testSuiteId: string; environmentId: string; caseIds: string[]; tags: string[]; options: RunOptions }
export interface RunConfiguration { pageUrl?: string | null;
  schemaVersion: number; projectName: string; suiteName: string; suiteVersion: string;
  environmentName: string; baseUrl: string; environmentVersion: string;
  cases: { id: string; stableKey: string; name: string; catalogVersion: number; tags: string[] }[];
  tags: string[]; options: RunOptions;
}
export interface TestRun { createdByName?: string | null; enqueuedByName?: string | null; cancelledByName?: string | null; presetId?: string | null; presetRevision?: number | null; id: string; projectId: string; status: RunStatus; version: string; createdAt: string; startedAt: string | null; finishedAt: string | null; configuration: RunConfiguration; runnerAvailable: boolean; cancellationRequested?: boolean; runnerError?: string; progress?: { kind: string; key?: string; browser?: string; attempt?: number; status?: string }[]; result?: { passed: number; failed: number; skipped: number; total: number } }
export const runApi = {
  capabilities: (signal?: AbortSignal) => apiRequest<{ enabled: boolean; catalog: { key: string; name: string; types: string[] }[] }>(`/runner`, { signal }),
  enqueue: (run: TestRun) => apiRequest<TestRun>(`/test-runs/${run.id}/enqueue`, { method: 'POST', body: JSON.stringify({ version: run.version }) }),
  list: (projectId: string, status: string, page: number, signal?: AbortSignal) => apiRequest<{ items: TestRun[]; total: number }>(`/test-runs?${new URLSearchParams({ ...(projectId ? { projectId } : {}), status, page: String(page), pageSize: '12' })}`, { signal }),
  get: (id: string, signal?: AbortSignal) => apiRequest<TestRun>(`/test-runs/${encodeURIComponent(id)}`, { signal }),
  create: (projectId: string, input: RunInput) => apiRequest<TestRun>(`/projects/${projectId}/test-runs`, { method: 'POST', body: JSON.stringify(input) }),
  cancel: (run: TestRun) => apiRequest<TestRun>(`/test-runs/${run.id}/cancel`, { method: 'POST', body: JSON.stringify({ version: run.version }) }),
};
export function validateOptions(options: RunOptions): string | null {
  if (!Number.isInteger(options.workers) || options.workers < 1 || options.workers > 10) return 'Workers deve estar entre 1 e 10.';
  if (!Number.isInteger(options.retries) || options.retries < 0 || options.retries > 5) return 'Retries deve estar entre 0 e 5.';
  if (!Number.isInteger(options.timeoutSeconds) || options.timeoutSeconds < 5 || options.timeoutSeconds > 300) return 'Timeout deve estar entre 5 e 300 segundos.';
  return null;
}
