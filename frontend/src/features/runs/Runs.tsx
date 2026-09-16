import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectsApi } from '../projects/api';
import { suiteApi, parseTags } from '../suites/api';
import { catalogApi, type TestCase } from '../catalog/api';
import { defaultOptions, runApi, statuses, statusLabels, validateOptions, type RunOptions, type RunConfiguration } from './api';

const runnerNote = 'As configurações são salvas como pendentes. Execute explicitamente nos detalhes quando o runner estiver habilitado no servidor.';
function ErrorNotice({ error }: { error: Error }) { return <p className="error-message" role="alert">{error.message}</p>; }
function Pages({ page, total, change }: { page: number; total: number; change: (page: number) => void }) {
  return <div className="pagination"><button type="button" className="button" disabled={page === 1} onClick={() => change(page - 1)}>Anterior</button><span>Página {page}</span><button type="button" className="button" disabled={page * 12 >= total} onClick={() => change(page + 1)}>Próxima</button></div>;
}
function OptionsSummary({ options }: { options: RunOptions }) {
  return <dl className="run-summary">{Object.entries({ Tipo: options.testType, Navegador: options.browser, Modo: options.mode, Workers: options.workers, Retries: options.retries, 'Timeout (s)': options.timeoutSeconds, Screenshot: options.screenshot, Vídeo: options.video, Trace: options.trace }).map(([key, value]) => <div key={key}><dt>{key}</dt><dd>{value}</dd></div>)}</dl>;
}
function Snapshot({ configuration: c }: { configuration: RunConfiguration }) {
  return <section className="project-form"><h2>Configuração salva</h2><p>{c.projectName} / {c.suiteName}</p><p>{c.environmentName} · {c.baseUrl}</p><OptionsSummary options={c.options} /><p>Tags: {c.tags.join(' ') || 'Sem filtro de tags'}</p><h3>Casos selecionados ({c.cases.length})</h3><ul>{c.cases.map(x => <li key={x.id}>{x.name} — <code>{x.stableKey}</code> · revisão {x.catalogVersion}</li>)}</ul></section>;
}

export function RunHistory() {
  const { projectId = '' } = useParams();
  const [status, setStatus] = useState('all'); const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['runs', projectId, status, page], queryFn: ({ signal }) => runApi.list(projectId, status, page, signal) });
  return <>
    <div className="projects-title"><div className="page-title"><p className="eyebrow">EXECUÇÕES</p><h1>Histórico de execuções</h1><p>Configurações e estados preservados por solicitação.</p></div><Link className="button primary" to={projectId ? `/projects/${projectId}/test-runs/new` : '/projects'}>{projectId ? 'Nova execução' : 'Escolher projeto'}</Link></div>
    <p className="status-message">{runnerNote}</p>
    <div className="field"><label htmlFor="run-filter">Status da execução</label><select id="run-filter" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="all">Todos</option>{statuses.map(x => <option key={x} value={x}>{statusLabels[x]}</option>)}</select></div>
    {query.isPending ? <p role="status">Carregando execuções…</p> : query.isError ? <><ErrorNotice error={query.error} /><button className="button" onClick={() => void query.refetch()}>Tentar novamente</button></> : <>
      <p role="status">{query.data.total} execuções encontradas</p>{!query.data.total && <p className="empty-projects">Nenhuma execução neste filtro.</p>}
      <div className="project-grid">{query.data.items.map(run => <article className="project-card" key={run.id}><span className="project-badge">{statusLabels[run.status]}</span><h2>{run.configuration.suiteName}</h2><p>{run.configuration.projectName} · {run.configuration.environmentName}</p><p>{new Date(run.createdAt).toLocaleString('pt-BR')}</p><p>{run.configuration.cases.length} casos · {run.configuration.options.browser}</p><Link className="button" to={`/test-runs/${run.id}`}>Ver execução</Link></article>)}</div>
      <Pages page={page} total={query.data.total} change={setPage} />
    </>}
  </>;
}

export function RunDetails() {
  const { id = '' } = useParams(); const client = useQueryClient(); const [confirm, setConfirm] = useState(false);
  const query = useQuery({ queryKey: ['run', id], queryFn: ({ signal }) => runApi.get(id, signal), refetchInterval: query => ['Queued', 'Running'].includes(query.state.data?.status ?? '') ? 2000 : false });
  const [enqueueConfirm, setEnqueueConfirm] = useState(false);
  const enqueue = useMutation({ mutationFn: () => runApi.enqueue(query.data!), onSuccess: async run => { client.setQueryData(['run', id], run); await client.invalidateQueries({ queryKey: ['runs'] }); setEnqueueConfirm(false); } });
  const mutation = useMutation({ mutationFn: () => runApi.cancel(query.data!), onSuccess: async run => { client.setQueryData(['run', id], run); await client.invalidateQueries({ queryKey: ['runs'] }); setConfirm(false); } });
  if (query.isPending) return <p role="status">Carregando execução…</p>;
  if (query.isError) return <><ErrorNotice error={query.error} /><button className="button" onClick={() => void query.refetch()}>Tentar novamente</button></>;
  const run = query.data;
  return <><Link className="back-link" to={`/projects/${run.projectId}/test-runs`}>← Voltar ao histórico</Link><div className="page-title"><h1>Detalhes da execução</h1><p role="status">{statusLabels[run.status]}</p><p>Criada em {new Date(run.createdAt).toLocaleString('pt-BR')}</p>{run.finishedAt && <p>Encerrada em {new Date(run.finishedAt).toLocaleString('pt-BR')}</p>}</div>
    {!run.runnerAvailable && <p className="status-message">Runner desabilitado no servidor. Nenhum teste é executado enquanto ele estiver desabilitado.</p>}
    {run.status === 'Pending' && run.runnerAvailable && <div className="form-actions">{enqueueConfirm ? <><p>Executar os casos desta configuração no ambiente indicado? O catálogo será revalidado.</p><button className="button primary" disabled={enqueue.isPending} onClick={() => enqueue.mutate()}>Confirmar execução</button><button className="button" onClick={() => setEnqueueConfirm(false)}>Voltar</button></> : <button className="button primary" onClick={() => setEnqueueConfirm(true)}>Executar agora</button>}</div>}
    {enqueue.isError && <><ErrorNotice error={enqueue.error} /><button className="button" onClick={() => { enqueue.reset(); void query.refetch(); }}>Recarregar execução</button></>}
    {run.cancellationRequested && run.status === 'Running' && <p role="status">Cancelamento solicitado. Aguardando encerramento do processo.</p>}
    {run.runnerError && <p className="error-message">{run.runnerError}</p>}
    {run.progress && run.progress.length > 0 && <section className="project-form"><h2>Progresso real</h2><p>{run.progress.filter(x => x.kind === 'attempt').length} tentativas concluídas (inclui retries).</p><ul>{run.progress.filter(x => x.kind === 'attempt').slice(-10).map((x, i) => <li key={i}>{x.key} · {x.browser} · tentativa {(x.attempt ?? 0) + 1} · {x.status}</li>)}</ul></section>}
    {run.result && <p className="status-message">{run.result.passed} aprovados · {run.result.failed} falhos · {run.result.skipped} ignorados · {run.result.total} testes</p>}
    {!run.cancellationRequested && ['Pending', 'Queued', 'Running'].includes(run.status) && <div className="form-actions">{confirm ? <><p>Confirmar o cancelamento desta solicitação?</p><button className="button danger" disabled={mutation.isPending} onClick={() => mutation.mutate()}>Confirmar cancelamento</button><button className="button" disabled={mutation.isPending} onClick={() => setConfirm(false)}>Voltar</button></> : <button className="button" onClick={() => setConfirm(true)}>Cancelar execução</button>}</div>}
    {mutation.isError && <><ErrorNotice error={mutation.error} /><button className="button" onClick={() => { mutation.reset(); setConfirm(false); void query.refetch(); }}>Recarregar estado</button></>}
    <Snapshot configuration={run.configuration} />
  </>;
}

export function RunWizard() {
  const { projectId = '' } = useParams(); const navigate = useNavigate(); const client = useQueryClient();
  const [step, setStep] = useState(1); const [suiteId, setSuiteId] = useState(''); const [suiteName, setSuiteName] = useState('');
  const [suitePage, setSuitePage] = useState(1); const [casePage, setCasePage] = useState(1);
  const [selected, setSelected] = useState<TestCase[]>([]); const [environmentId, setEnvironmentId] = useState('');
  const [tags, setTags] = useState(''); const [options, setOptions] = useState<RunOptions>({ ...defaultOptions }); const [error, setError] = useState<string | null>(null);
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId, signal) });
  const suites = useQuery({ queryKey: ['suites', projectId, 'wizard', suitePage], queryFn: ({ signal }) => suiteApi.list(projectId, '', 'active', suitePage, signal) });
  const cases = useQuery({ queryKey: ['cases', suiteId, 'wizard', casePage], queryFn: ({ signal }) => catalogApi.cases(suiteId, '', 'active', casePage, signal), enabled: !!suiteId });
  const environments = useQuery({ queryKey: ['environments', projectId], queryFn: ({ signal }) => catalogApi.environments(projectId, signal) });
  const capabilities = useQuery({ queryKey: ['runner'], queryFn: ({ signal }) => runApi.capabilities(signal), retry: false });
  const environment = environments.data?.find(x => x.id === environmentId);
  const mutation = useMutation({ mutationFn: () => runApi.create(projectId, { testSuiteId: suiteId, environmentId, caseIds: selected.map(x => x.id), tags: parseTags(tags), options }), onSuccess: async run => { client.setQueryData(['run', run.id], run); await Promise.all([client.invalidateQueries({ queryKey: ['runs'] }), client.invalidateQueries({ queryKey: ['project', projectId] }), client.invalidateQueries({ queryKey: ['projects'] })]); navigate(`/test-runs/${run.id}`); } });
  function submit(event: FormEvent) {
    event.preventDefault(); if (mutation.isPending || !project.data || project.data.archivedAt) return;
    const parsed = parseTags(tags);
    const validation = step === 1 && !suiteId ? 'Selecione uma suíte ativa.' : step === 2 && (!selected.length || selected.length > 100) ? 'Selecione de 1 a 100 casos.' :
      step === 2 && (parsed.length > 20 || parsed.some(x => !/^@[a-z0-9][a-z0-9_-]{0,39}$/.test(x))) ? 'Informe até 20 tags válidas, como @smoke.' :
      step === 2 && parsed.length > 0 && selected.some(x => !parsed.some(tag => x.tags.includes(tag))) ? 'Cada caso deve conter ao menos uma das tags informadas.' :
      step >= 3 && (!environment?.enabled || environment.name === 'Production') ? 'Selecione um ambiente habilitado.' : step >= 4 ? validateOptions(options) : null;
    setError(validation); if (validation) return;
    if (step < 5) setStep(step + 1); else mutation.mutate();
  }
  const failures = [project.error, suites.error, cases.error, environments.error].filter(Boolean) as Error[];
  return <><Link className="back-link" to={`/projects/${projectId}`}>← Voltar ao projeto</Link><div className="page-title"><p className="eyebrow">NOVA EXECUÇÃO</p><h1>Configurar execução</h1><p>{project.data?.name}</p></div><p className="status-message">{runnerNote}</p>
    {capabilities.data?.enabled && <p className="roadmap-note">Catálogo executável: {capabilities.data.catalog.map(x => `${x.key} (${x.types.join(', ')})`).join('; ')}. Use essas chaves nos casos cadastrados.</p>}
    {failures.map((failure, index) => <ErrorNotice key={index} error={failure} />)}{failures.length > 0 && <button className="button" onClick={() => { void project.refetch(); void suites.refetch(); void environments.refetch(); if (suiteId) void cases.refetch(); }}>Recarregar catálogo</button>}
    {project.data?.archivedAt && <p className="error-message">Projetos arquivados não permitem novas execuções.</p>}
    <ol className="run-steps" aria-label="Etapas da configuração">{['Suíte', 'Casos', 'Ambiente', 'Opções', 'Revisão'].map((label, index) => <li key={label} aria-current={step === index + 1 ? 'step' : undefined}>{index + 1}. {label}</li>)}</ol>
    <form className="project-form" onSubmit={submit} noValidate><fieldset className="catalog-fields" disabled={mutation.isPending || !project.data || !!project.data.archivedAt}>
      <h2>Etapa {step} de 5</h2>
      {step === 1 && <><h3>Escolha uma suíte ativa</h3>{suites.isPending && <p role="status">Carregando suítes…</p>}{suites.data?.items.map(suite => <label className="run-choice" key={suite.id}><input type="radio" name="suite" checked={suiteId === suite.id} onChange={() => { setSuiteId(suite.id); setSuiteName(suite.name); setSelected([]); setCasePage(1); }} />{suite.name}</label>)}{suites.data && <Pages page={suitePage} total={suites.data.total} change={setSuitePage} />}{suites.data?.total === 0 && <p>Nenhuma suíte ativa. <Link to={`/projects/${projectId}/test-suites`}>Gerenciar suítes</Link></p>}</>}
      {step === 2 && <><h3>Selecione os casos ({selected.length}/100)</h3>{cases.isPending && <p role="status">Carregando casos…</p>}{cases.data?.items.map(item => <label className="run-choice" key={item.id}><input type="checkbox" checked={selected.some(x => x.id === item.id)} disabled={selected.length >= 100 && !selected.some(x => x.id === item.id)} onChange={e => setSelected(e.target.checked ? [...selected, item] : selected.filter(x => x.id !== item.id))} />{item.name} · {item.stableKey}</label>)}{cases.data && <Pages page={casePage} total={cases.data.total} change={setCasePage} />}{cases.data?.total === 0 && <p>Nenhum caso ativo. <Link to={`/test-suites/${suiteId}/test-cases`}>Gerenciar casos</Link></p>}<div className="field"><label htmlFor="run-tags">Tags (opcional)</label><input id="run-tags" maxLength={1000} value={tags} onChange={e => setTags(e.target.value)} /><p className="field-help">Cada caso selecionado deve conter ao menos uma dessas tags. Sem tags, todos os casos selecionados são incluídos.</p></div></>}
      {step === 3 && <><h3>Escolha o ambiente</h3>{environments.isPending && <p role="status">Carregando ambientes…</p>}{environments.data?.map(item => <label className="run-choice" key={item.id}><input type="radio" name="environment" disabled={!item.enabled || item.name === 'Production'} checked={environmentId === item.id} onChange={() => setEnvironmentId(item.id)} />{item.name} · {item.baseUrl}{!item.enabled && ' (desabilitado)'}</label>)}<Link to={`/projects/${projectId}/environments`}>Gerenciar ambientes</Link></>}
      {step === 4 && <><h3>Opções de execução</h3>{Object.entries({ testType: ['Smoke', 'Regression', 'EndToEnd', 'API', 'Accessibility'], browser: ['Chromium', 'Firefox', 'WebKit', 'All'], mode: ['Headless', 'Headed'], screenshot: ['Always', 'OnFailure', 'Never'], video: ['Always', 'OnFailure', 'Never'], trace: ['Always', 'OnFailure', 'Never'] }).map(([key, values]) => <div className="field" key={key}><label htmlFor={`option-${key}`}>{({ testType: 'Tipo de teste', browser: 'Navegador', mode: 'Modo', screenshot: 'Screenshot', video: 'Vídeo', trace: 'Trace' } as Record<string, string>)[key]}</label><select id={`option-${key}`} value={String(options[key as keyof RunOptions])} onChange={e => setOptions({ ...options, [key]: e.target.value })}>{values.map(value => <option key={value}>{value}</option>)}</select></div>)}{(['workers', 'retries', 'timeoutSeconds'] as const).map(key => <div className="field" key={key}><label htmlFor={`option-${key}`}>{({ workers: 'Workers', retries: 'Retries', timeoutSeconds: 'Timeout (segundos)' })[key]}</label><input id={`option-${key}`} type="number" min={key === 'workers' ? 1 : key === 'retries' ? 0 : 5} max={key === 'workers' ? 10 : key === 'retries' ? 5 : 300} value={options[key]} onChange={e => setOptions({ ...options, [key]: Number(e.target.value) })} /></div>)}<p className="field-help">Always = sempre; OnFailure = apenas em falha; Never = nunca. All inclui os três navegadores. Headed exigirá display no worker. Tipo classifica testes existentes.</p></>}
      {step === 5 && <><h3>Revise antes de salvar</h3><p>{project.data?.name} / {suiteName}</p><p>{environment?.name} · {environment?.baseUrl}</p><OptionsSummary options={options} /><p>Tags: {tags || 'Sem filtro'}</p><ul>{selected.map(item => <li key={item.id}>{item.name} · {item.stableKey} · revisão {item.catalogVersion}</li>)}</ul><p>A configuração será imutável após salvar. Status inicial: Pendente.</p></>}
      {error && <p className="field-error" role="alert">{error}</p>}{mutation.isError && <ErrorNotice error={mutation.error} />}
      <div className="form-actions">{step > 1 && <button type="button" className="button" onClick={() => { setError(null); setStep(step - 1); }}>Etapa anterior</button>}<button className="button primary" disabled={failures.length > 0}>{mutation.isPending ? 'Salvando…' : step === 5 ? 'Salvar como pendente' : 'Continuar'}</button></div>
    </fieldset></form>
  </>;
}
