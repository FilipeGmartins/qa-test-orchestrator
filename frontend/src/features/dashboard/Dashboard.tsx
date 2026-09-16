import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { dashboardApi } from './api';
import { projectsApi } from '../projects/api';
import { statusLabels, statuses } from '../runs/api';

const dateString = (date: Date) => date.toISOString().slice(0, 10);
export function Dashboard() {
  const [initial] = useState(() => { const today = new Date(); const from = new Date(today); from.setUTCDate(from.getUTCDate() - 29); return { from: dateString(from), to: dateString(today), projectId: '' }; });
  const [filter, setFilter] = useState(initial); const [draft, setDraft] = useState(initial);
  const [error, setError] = useState(''); const [search, setSearch] = useState(''); const [projectPage, setProjectPage] = useState(1);
  const projects = useQuery({ queryKey: ['dashboard-projects', search, projectPage], queryFn: ({ signal }) => projectsApi.list(search, 'all', projectPage, signal) });
  const query = useQuery({ queryKey: ['dashboard', filter], queryFn: ({ signal }) => dashboardApi.get(filter.projectId, filter.from, filter.to, signal), refetchInterval: 30000 });
  function submit(event: FormEvent) {
    event.preventDefault();
    const days = (Date.parse(draft.to) - Date.parse(draft.from)) / 86400000;
    if (!draft.from || !draft.to || !Number.isFinite(days) || days < 0 || days >= 90 || draft.to > dateString(new Date())) { setError('Selecione de 1 a 90 dias, sem datas futuras.'); return; }
    setError(''); setFilter({ ...draft });
  }
  const data = query.data;
  return <><div className="projects-title"><div className="page-title"><p className="eyebrow">MÉTRICAS REAIS</p><h1>Dashboard</h1><p>Acompanhe execuções, resultados finais e falhas recuperadas em retries.</p></div><button className="button" disabled={query.isFetching} onClick={() => void query.refetch()}>Atualizar métricas</button></div>
    <form className="project-form" onSubmit={submit}>
      <div className="result-filters"><div className="field"><label htmlFor="dashboard-from">De (UTC)</label><input id="dashboard-from" type="date" value={draft.from} onChange={e => setDraft({ ...draft, from: e.target.value })} /></div>
        <div className="field"><label htmlFor="dashboard-to">Até (UTC)</label><input id="dashboard-to" type="date" value={draft.to} max={dateString(new Date())} onChange={e => setDraft({ ...draft, to: e.target.value })} /></div>
        <div className="field"><label htmlFor="dashboard-search">Buscar projeto</label><input id="dashboard-search" value={search} maxLength={120} onChange={e => { setSearch(e.target.value); setProjectPage(1); }} /></div>
        <div className="field"><label htmlFor="dashboard-project">Projeto</label><select id="dashboard-project" value={draft.projectId} onChange={e => setDraft({ ...draft, projectId: e.target.value })}><option value="">Todos os projetos</option>{draft.projectId && !projects.data?.items.some(x => x.id === draft.projectId) && <option value={draft.projectId}>Projeto selecionado</option>}{projects.data?.items.map(x => <option value={x.id} key={x.id}>{x.name}{x.archivedAt ? ' (arquivado)' : ''}</option>)}</select></div></div>
      {projects.isError && <p role="alert">Não foi possível carregar projetos. <button type="button" className="button" onClick={() => void projects.refetch()}>Recarregar projetos</button></p>}
      {projects.data && projects.data.total > 12 && <div className="pagination"><button type="button" className="button" disabled={projectPage === 1} onClick={() => setProjectPage(projectPage - 1)}>Projetos anteriores</button><span>Página {projectPage}</span><button type="button" className="button" disabled={projectPage * 12 >= projects.data.total} onClick={() => setProjectPage(projectPage + 1)}>Mais projetos</button></div>}
      {error && <p role="alert" className="error-message">{error}</p>}<button className="button primary">Aplicar filtros</button>
    </form>
    {query.isPending ? <p role="status">Carregando métricas…</p> : query.isError ? <p className="error-message" role="alert">{query.error.message} Os indicadores não estão disponíveis; tente atualizar.</p> : data && <>
      <p className="last-checked">Período: {filter.from} a {filter.to} (UTC), pela criação da execução. Atualizado em {new Date(data.generatedAt).toLocaleString('pt-BR')}.</p>
      {data.totalRuns === 0 && <p className="empty-projects">Nenhuma execução neste período. <Link to="/projects">Escolher projeto para executar testes</Link></p>}
      <div className="dashboard-metrics">{[
        ['Execuções', data.totalRuns], ['Testes com resultado', data.passed + data.failed + data.skipped + data.interrupted], ['Testes aprovados', data.passed], ['Testes falhos', data.failed], ['Testes ignorados', data.skipped],
        ['Taxa de sucesso', `${data.successRate.toLocaleString('pt-BR')}%`], ['Duração média por teste', data.averageTestDurationMs === null ? 'Sem avaliações' : `${(data.averageTestDurationMs / 1000).toLocaleString('pt-BR')} s`],
        ['Recuperados em retry', data.flakyTests],
      ].map(([label, value]) => <article className="project-card" key={label}><h2>{label}</h2><strong className="metric-value">{value}</strong></article>)}</div>
      <p className="roadmap-note">Testes: última tentativa de cada caso/navegador em execuções aprovadas ou reprovadas. Sucesso = aprovados ÷ (aprovados + falhos); ignorados e interrompidos ficam fora. Sem avaliações, a taxa é 0%. Duração média considera somente a última tentativa avaliada.</p>
      {data.completedRunsWithoutDetails > 0 && <p className="status-message">{data.completedRunsWithoutDetails} execuções concluídas sem detalhes de tentativas não entram nas métricas de testes.</p>}
      {data.interrupted > 0 && <p>{data.interrupted} testes interrompidos, fora da taxa de sucesso.</p>}
      <section className="project-form"><h2>Estados das execuções</h2><div className="suite-tags">{statuses.map(s => <span key={s}>{statusLabels[s]}: {data.runCounts[s]}</span>)}</div></section>
      <section className="project-form"><h2>Evolução diária</h2><p>Resultados finais por dia de criação da execução (UTC).</p><div className="dashboard-chart"><ResponsiveContainer width="100%" height={300}><LineChart data={data.daily} accessibilityLayer><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="date" tickFormatter={value => String(value).slice(5)} /><YAxis allowDecimals={false} /><Tooltip /><Legend /><Line type="monotone" dataKey="passed" name="Aprovados" stroke="#23734d" isAnimationActive={false} /><Line type="monotone" dataKey="failed" name="Falhos" stroke="#b94135" isAnimationActive={false} /><Line type="monotone" dataKey="skipped" name="Ignorados" stroke="#76640d" isAnimationActive={false} /></LineChart></ResponsiveContainer></div>
        <details><summary>Consultar valores diários em texto</summary><ul>{data.daily.map(d => <li key={d.date}>{d.date}: {d.runs} execuções · {d.passed} aprovados · {d.failed} falhos · {d.skipped} ignorados</li>)}</ul></details></section>
      <div className="dashboard-columns"><section className="project-form"><h2>Execuções recentes</h2>{!data.recentRuns.length && <p>Nenhuma execução no período.</p>}{data.recentRuns.map(r => <article className="dashboard-row" key={r.id}><Link to={`/test-runs/${r.id}`}>{r.projectName} / {r.suiteName}</Link><p>{statusLabels[r.status]} · {new Date(r.createdAt).toLocaleString('pt-BR')}</p></article>)}</section>
        <section className="project-form"><h2>Falhas recentes</h2>{!data.recentFailures.length && <p>Nenhuma falha final registrada neste período.</p>}{data.recentFailures.map(f => <article className="dashboard-row" key={`${f.runId}-${f.caseId}-${f.browser}`}><Link to={`/test-runs/${f.runId}`}>{f.caseName} · {f.browser}</Link><p>{f.error || 'Consulte os detalhes da execução.'}</p></article>)}</section></div>
      <section className="project-form"><h2>Instabilidade: aprovação após retry</h2><p>Casos que falharam ou excederam o tempo e depois passaram na mesma execução. Até oito casos, ordenados pelo número de recuperações.</p>{!data.flakyCases.length && <p>Nenhuma recuperação em retry neste período.</p>}{data.flakyCases.map(f => <article className="dashboard-row" key={`${f.caseId}-${f.browser}`}><Link to={`/test-cases/${f.caseId}/results`}>{f.caseName} · {f.browser}</Link><p>{f.recoveredRuns} execuções recuperadas</p></article>)}</section>
    </>}
  </>;
}
