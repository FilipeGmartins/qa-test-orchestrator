export class ApiError extends Error {
  constructor(message: string, public code: string, public status: number, public field?: string) { super(message); }
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    const headers = new Headers(options.headers);
    headers.set('Accept', 'application/json');
    if (options.body) headers.set('Content-Type', 'application/json');
    if (!['GET', 'HEAD', 'OPTIONS'].includes((options.method ?? 'GET').toUpperCase())) {
      const csrf = await fetch('/api/auth/csrf', { credentials: 'same-origin', signal: AbortSignal.timeout(15000) });
      if (!csrf.ok) throw new Error('CSRF unavailable');
      headers.set('X-CSRF-Token', (await csrf.json()).token);
    }
    response = await fetch(`/api${path}`, {
      ...options,
      headers, credentials: 'same-origin',
      signal: AbortSignal.any([...(options.signal ? [options.signal] : []), AbortSignal.timeout(15_000)]),
    });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError('Não foi possível conectar à API. Confira a conexão e consulte a lista antes de repetir um cadastro.', 'NETWORK_ERROR', 0);
  }
  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/auth/')) window.dispatchEvent(new Event('qa:unauthorized'));
    const body = await response.json().catch(() => null);
    throw new ApiError(body?.message ?? 'Não foi possível concluir a solicitação.', body?.error ?? 'REQUEST_FAILED', response.status, body?.field);
  }
  return response.json() as Promise<T>;
}
