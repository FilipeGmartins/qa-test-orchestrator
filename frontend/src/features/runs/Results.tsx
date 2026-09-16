import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../api/http';

export interface AttemptResult {
  id: string; runId: string; caseId: string; caseName: string; stableKey: string;
  browser: string; attempt: number; status: string; durationMs: number;
  error: string; stack: string; logs: string; recordedAt: string;
  artifacts: { id: string; kind: string; size: number; expiresAt: string; available: boolean }[];
}
export const resultsApi = {
  list: (scope: 'test-runs' | 'test-cases', id: string, status: string, browser: string, page: number, signal?: AbortSignal) =>
    apiRequest<{ items: AttemptResult[]; total: number }>(`/${scope}/${encodeURIComponent(id)}/results?${new URLSearchParams({ status, browser, page: String(page), pageSize: '12' })}`, { signal }),
};
const labels: Record<string, string> = { passed: 'Aprovado', failed: 'Falhou', timedOut: 'Tempo esgotado', skipped: 'Ignorado', interrupted: 'Interrompido' };
const kinds: Record<string, string> = { screenshot: 'Screenshot', video: 'Vídeo', trace: 'Trace' };

export function CaseResults() {
  const { id = '' } = useParams();
  return <><div className="page-title"><p className="eyebrow">HISTÓRICO POR CASO</p><h1>Resultados do caso</h1><p>Tentativas preservadas entre execuções, incluindo retries.</p></div><ResultsPanel id={id} scope="test-cases" /></>;
}

export function ResultsPanel({ id, scope = 'test-runs', active = false }: { id: string; scope?: 'test-runs' | 'test-cases'; active?: boolean }) {
  const [status, setStatus] = useState('all'); const [browser, setBrowser] = useState('all'); const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['results', scope, id, status, browser, page, active], queryFn: ({ signal }) => resultsApi.list(scope, id, status, browser, page, signal), refetchInterval: active ? 2000 : false });
  return <section className="project-form"><h2>Resultados e evidências</h2><p>Cada registro corresponde a uma tentativa. Os totais da execução consideram o resultado final de cada teste.</p>
    <div className="result-filters"><div className="field"><label htmlFor="result-status">Resultado da tentativa</label><select id="result-status" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="all">Todos</option>{Object.entries(labels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
      <div className="field"><label htmlFor="result-browser">Navegador da tentativa</label><select id="result-browser" value={browser} onChange={e => { setBrowser(e.target.value); setPage(1); }}><option value="all">Todos</option>{['Chromium', 'Firefox', 'WebKit'].map(x => <option key={x}>{x}</option>)}</select></div></div>
    {query.isPending ? <p>Carregando resultados…</p> : query.isError ? <><p className="error-message" role="alert">{query.error.message}</p><button className="button" onClick={() => void query.refetch()}>Recarregar resultados</button></> : <>
      <p>{query.data.total} tentativas encontradas</p>{!query.data.total && <p>Nenhuma tentativa registrada neste filtro. Execuções anteriores à versão 0.7 podem ter somente o resumo de progresso.</p>}
      {query.data.items.map(item => <article className="result-attempt" key={item.id}>
        <div className="section-heading"><h3>{item.caseName}</h3><span className="project-badge">{labels[item.status] ?? item.status}</span></div>
        <p><code>{item.stableKey}</code> · {item.browser} · tentativa {item.attempt + 1} · {(item.durationMs / 1000).toLocaleString('pt-BR')} s</p>
        <p>{new Date(item.recordedAt).toLocaleString('pt-BR')}</p>
        <Link to={scope === 'test-runs' ? `/test-cases/${item.caseId}/results` : `/test-runs/${item.runId}`}>{scope === 'test-runs' ? 'Histórico deste caso' : 'Ver execução'}</Link>
        {item.error && <pre className="result-log" aria-label="Mensagem de erro">{item.error}</pre>}
        {item.stack && <details><summary>Stack trace</summary><pre className="result-log">{item.stack}</pre></details>}
        {item.logs && <details><summary>Logs da tentativa</summary><pre className="result-log">{item.logs}</pre></details>}
        <h4>Evidências</h4>{!item.artifacts.length ? <p>Nenhuma evidência capturada nesta tentativa.</p> : <ul>{item.artifacts.map(a => <li key={a.id}>{a.available ? <a href={`/api/test-runs/${item.runId}/artifacts/${a.id}`}>Baixar {kinds[a.kind] ?? a.kind}</a> : <span>{kinds[a.kind] ?? a.kind} indisponível ou expirado</span>} · {(a.size / 1024).toFixed(1)} KB · expira em {new Date(a.expiresAt).toLocaleString('pt-BR')}</li>)}</ul>}
      </article>)}
      <div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button className="button" disabled={page * 12 >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div>
    </>}
  </section>;
}
