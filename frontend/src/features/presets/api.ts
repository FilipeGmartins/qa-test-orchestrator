import { apiRequest } from '../../api/http';
import type { RunInput, RunConfiguration, TestRun } from '../runs/api';
export interface Preset { id: string; projectId: string; name: string; description: string; revision: number; version: string; archived: boolean; createdAt: string; updatedAt: string; configuration: RunInput; snapshot: RunConfiguration }
export interface PresetRevision { revision: number; name: string; description: string; createdAt: string; configuration: RunInput; snapshot: RunConfiguration }
export interface PresetPreview { version: string; fingerprint: string; configuration: RunConfiguration; revision: number; name: string }
export const presetApi = {
  list: (projectId: string, search: string, status: string, page: number, signal?: AbortSignal) => apiRequest<{ items: Preset[]; total: number }>(`/projects/${projectId}/presets?${new URLSearchParams({ search, status, page: String(page), pageSize: '12' })}`, { signal }),
  get: (id: string, signal?: AbortSignal) => apiRequest<Preset>(`/presets/${id}`, { signal }),
  save: (projectId: string, input: { name: string; description: string; configuration: RunInput }, preset?: Preset) => apiRequest<Preset>(`/projects/${projectId}/presets${preset ? `/${preset.id}` : ''}`, { method: preset ? 'PUT' : 'POST', body: JSON.stringify({ ...input, version: preset?.version }) }),
  preview: (id: string) => apiRequest<PresetPreview>(`/presets/${id}/preview`),
  use: (id: string, preview: PresetPreview) => apiRequest<TestRun>(`/presets/${id}/runs`, { method: 'POST', body: JSON.stringify({ version: preview.version, fingerprint: preview.fingerprint }) }),
  archive: (preset: Preset) => apiRequest<Preset>(`/presets/${preset.id}/archive`, { method: 'POST', body: JSON.stringify({ version: preset.version }) }),
  revisions: (id: string, page: number, signal?: AbortSignal) => apiRequest<{ items: PresetRevision[]; total: number }>(`/presets/${id}/revisions?page=${page}&pageSize=12`, { signal }),
};
