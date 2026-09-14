import { apiRequest } from '../../api/http';

export type SuiteStatus = 'Active' | 'Inactive';
export interface SuiteInput { name: string; description: string; tags: string[]; status: SuiteStatus }
export interface Suite extends SuiteInput { id: string; projectId: string; createdAt: string; updatedAt: string; version: string }
export interface SuitePage { items: Suite[]; total: number; page: number; pageSize: number }
export const suiteApi = {
  list: (projectId: string, search: string, status: string, page: number, signal?: AbortSignal) => apiRequest<SuitePage>(`/projects/${encodeURIComponent(projectId)}/test-suites?${new URLSearchParams({ search, status, page: String(page), pageSize: '12' })}`, { signal }),
  get: (id: string, signal?: AbortSignal) => apiRequest<Suite>(`/test-suites/${encodeURIComponent(id)}`, { signal }),
  create: (projectId: string, input: SuiteInput) => apiRequest<Suite>(`/projects/${encodeURIComponent(projectId)}/test-suites`, { method: 'POST', body: JSON.stringify(input) }),
  update: (suite: Suite, input: SuiteInput) => apiRequest<Suite>(`/test-suites/${encodeURIComponent(suite.id)}`, { method: 'PUT', body: JSON.stringify({ ...input, version: suite.version }) }),
};
export const parseTags = (value: string) => value.trim() ? value.split(/[\s,]+/).filter(Boolean).map(tag => tag.toLowerCase()) : [];
export function validateSuite(input: SuiteInput): string | null {
  if (!input.name.trim() || input.name.trim().length > 120) return 'Informe um nome com 1 a 120 caracteres.';
  if (input.description.trim().length > 2000) return 'A descrição deve ter até 2000 caracteres.';
  if (input.tags.length > 20) return 'Informe no máximo 20 tags.';
  if (input.tags.some(tag => !/^@[a-z0-9][a-z0-9_-]{0,39}$/.test(tag))) return 'Use tags como @smoke ou @checkout, com até 40 caracteres após @.';
  if (input.status !== 'Active' && input.status !== 'Inactive') return 'Selecione um status válido.';
  return null;
}
