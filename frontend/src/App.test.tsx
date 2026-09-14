import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

const status = { application: 'QA Test Orchestrator', version: '0.1.0', api: 'available', database: 'available' };
const clients: QueryClient[] = [];
function mount(path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  clients.push(client);
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); });

describe('Fundação da plataforma', () => {
  it('mostra carregamento sem inventar métricas', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    mount();
    expect(screen.getByRole('status')).toHaveTextContent('Verificando conexão');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Uma base para testar melhor');
    expect(screen.queryByText('100%')).not.toBeInTheDocument();
  });
  it('mostra serviços disponíveis a partir da API', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(status))));
    mount();
    expect(await screen.findByText('Conexão com o banco confirmada')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Conectividade da plataforma' })).getAllByText('Disponível')).toHaveLength(3);
  });
  it('distingue banco indisponível de API indisponível', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...status, database: 'unavailable' }))));
    mount('/settings');
    expect(await screen.findByText('Indisponível')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it('permite tentar novamente após falha HTTP', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(new Response('', { status: 503 })).mockImplementation(() => Promise.resolve(new Response(JSON.stringify(status))));
    vi.stubGlobal('fetch', fetch);
    mount();
    expect(await screen.findByRole('alert')).toHaveTextContent('API indisponível');
    await userEvent.click(screen.getByRole('button', { name: 'Verificar agora' }));
    expect(await screen.findByText('Conexão com o banco confirmada')).toBeInTheDocument();
  });
  it('rejeita resposta fora do contrato', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ api: 'available' }))));
    mount();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
  it('não mantém status antigo como disponível quando uma atualização falha', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(status))).mockResolvedValue(new Response('', { status: 500 })));
    mount();
    await screen.findByText('Conexão com o banco confirmada');
    await userEvent.click(screen.getByRole('button', { name: 'Verificar agora' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText('Conexão com o banco confirmada')).not.toBeInTheDocument();
  });
});
