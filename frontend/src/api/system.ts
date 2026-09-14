export type Availability = 'available' | 'unavailable';
export interface SystemStatus {
  application: string;
  version: string;
  api: Availability;
  database: Availability;
}

export async function getSystemStatus(signal?: AbortSignal): Promise<SystemStatus> {
  const response = await fetch('/api/system', {
    signal: AbortSignal.any([...(signal ? [signal] : []), AbortSignal.timeout(10_000)]),
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) throw new Error('Não foi possível consultar a API.');
  const data: unknown = await response.json();
  if (!data || typeof data !== 'object' ||
    !('application' in data) || typeof data.application !== 'string' ||
    !('version' in data) || typeof data.version !== 'string' ||
    !('api' in data) || !['available', 'unavailable'].includes(String(data.api)) ||
    !('database' in data) || !['available', 'unavailable'].includes(String(data.database))) {
    throw new Error('A API retornou uma resposta inválida.');
  }
  return data as SystemStatus;
}
