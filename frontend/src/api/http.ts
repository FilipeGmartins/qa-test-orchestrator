export class ApiError extends Error {
  constructor(message: string, public code: string, public status: number, public field?: string) { super(message); }
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, {
      ...options,
      headers: { Accept: 'application/json', ...(options.body ? { 'Content-Type': 'application/json' } : {}) },
      signal: AbortSignal.any([...(options.signal ? [options.signal] : []), AbortSignal.timeout(15_000)]),
    });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError('Não foi possível conectar à API. Confira a conexão e consulte a lista antes de repetir um cadastro.', 'NETWORK_ERROR', 0);
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new ApiError(body?.message ?? 'Não foi possível concluir a solicitação.', body?.error ?? 'REQUEST_FAILED', response.status, body?.field);
  }
  return response.json() as Promise<T>;
}
