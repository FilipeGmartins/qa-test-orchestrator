import { apiRequest } from '../../api/http';
export { ApiError } from '../../api/http';

export interface Project {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
  version: string;
}
export interface ProjectPage { items: Project[]; total: number; page: number; pageSize: number }
export type ProjectStatus = 'active' | 'archived' | 'all';
export interface ProjectInput { name: string; description: string }
const request = <T>(path: string, options?: RequestInit) => apiRequest<T>(`/projects${path}`, options);

export const projectsApi = {
  list: (search: string, status: ProjectStatus, page: number, signal?: AbortSignal) =>
    request<ProjectPage>(`?${new URLSearchParams({ search, status, page: String(page), pageSize: '12' })}`, { signal }),
  get: (id: string, signal?: AbortSignal) => request<Project>(`/${encodeURIComponent(id)}`, { signal }),
  create: (input: ProjectInput) => request<Project>('', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: string, input: ProjectInput, version: string) => request<Project>(`/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify({ ...input, version }) }),
  archive: (id: string, version: string) => request<Project>(`/${encodeURIComponent(id)}/archive`, { method: 'POST', body: JSON.stringify({ version }) }),
};

export function validateProject(input: ProjectInput): Partial<Record<keyof ProjectInput, string>> {
  const errors: Partial<Record<keyof ProjectInput, string>> = {};
  if (!input.name.trim() || input.name.trim().length > 120) errors.name = 'Informe um nome com 1 a 120 caracteres.';
  if (input.description.trim().length > 2000) errors.description = 'A descrição deve ter até 2000 caracteres.';
  return errors;
}
