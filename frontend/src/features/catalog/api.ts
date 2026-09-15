import { apiRequest } from '../../api/http';
import type { SuiteInput } from '../suites/api';

export interface TestCase extends SuiteInput { id: string; testSuiteId: string; stableKey: string; catalogVersion: number; version: string }
export interface CaseInput extends SuiteInput { stableKey: string }
export interface CasePage { items: TestCase[]; total: number; page: number; pageSize: number }
export type EnvironmentName = 'Development' | 'Staging' | 'Production';
export interface EnvironmentInput { name: EnvironmentName; baseUrl: string; enabled: boolean }
export interface ProjectEnvironment extends EnvironmentInput { id: string; projectId: string; version: string }
export const catalogApi = {
  cases: (suiteId: string, search: string, status: string, page: number, signal?: AbortSignal) => apiRequest<CasePage>(`/test-suites/${encodeURIComponent(suiteId)}/test-cases?${new URLSearchParams({ search, status, page: String(page), pageSize: '12' })}`, { signal }),
  saveCase: (suiteId: string, input: CaseInput, existing?: TestCase) => apiRequest<TestCase>(existing ? `/test-cases/${existing.id}` : `/test-suites/${suiteId}/test-cases`, { method: existing ? 'PUT' : 'POST', body: JSON.stringify({ ...input, version: existing?.version }) }),
  environments: (projectId: string, signal?: AbortSignal) => apiRequest<ProjectEnvironment[]>(`/projects/${encodeURIComponent(projectId)}/environments`, { signal }),
  saveEnvironment: (projectId: string, input: EnvironmentInput, existing?: ProjectEnvironment) => apiRequest<ProjectEnvironment>(existing ? `/environments/${existing.id}` : `/projects/${projectId}/environments`, { method: existing ? 'PUT' : 'POST', body: JSON.stringify({ ...input, version: existing?.version }) }),
};
export function validateEnvironment(input: EnvironmentInput): string | null {
  try {
    const url = new URL(input.baseUrl.trim());
    if (input.baseUrl.trim().length > 2048 || !['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error();
  } catch { return 'Informe uma URL HTTP(S) sem credenciais, parâmetros ou fragmentos.'; }
  if (input.name === 'Production' && input.enabled) return 'Production permanece desabilitado até existir autorização específica.';
  return null;
}
