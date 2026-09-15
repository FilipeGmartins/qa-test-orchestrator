import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectsApi } from '../projects/api';
import { suiteApi, parseTags, validateSuite, type SuiteStatus } from '../suites/api';
import { catalogApi, validateEnvironment, type TestCase, type CaseInput, type EnvironmentInput, type EnvironmentName, type ProjectEnvironment } from './api';

function ErrorNotice({ error }: { error: Error }) {
  return <p className="error-message" role="alert">{error.message}</p>;
}

export function CaseCatalog() {
  const { suiteId = '' } = useParams();
  const [search, setSearch] = useState('');
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<TestCase | 'new' | null>(null);
  const suite = useQuery({ queryKey: ['suite', suiteId], queryFn: ({ signal }) => suiteApi.get(suiteId, signal) });
  const projectId = suite.data?.projectId;
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId!, signal), enabled: !!projectId });
  const query = useQuery({ queryKey: ['cases', suiteId, search, status, page], queryFn: ({ signal }) => catalogApi.cases(suiteId, search, status, page, signal) });
  const readOnly = !project.data || !!project.data.archivedAt;
  return <>
    <Link className="back-link" to={`/test-suites/${suiteId}`}>← Voltar à suíte</Link>
    <div className="projects-title"><div className="page-title"><p className="eyebrow">CATÁLOGO DE TESTES</p><h1>Casos de teste</h1><p>{suite.data?.name}</p></div>
      {!readOnly && <button className="button primary" onClick={() => setEditing('new')}>+ Novo caso</button>}</div>
    {suite.isError && <ErrorNotice error={suite.error} />}{project.isError && <ErrorNotice error={project.error} />}
    {project.data?.archivedAt && <p className="status-message">Projeto arquivado: apenas consulta.</p>}
    {suite.data?.status === 'Inactive' && <p className="status-message">Suíte inativa. O catálogo pode ser mantido, mas não poderá ser executado.</p>}
    <p className="roadmap-note">As chaves identificam os casos de forma permanente. A ligação com testes executáveis será feita na etapa do runner.</p>
    {editing && <CaseForm key={editing === 'new' ? 'new' : editing.version} suiteId={suiteId} projectId={projectId!} existing={editing === 'new' ? undefined : editing} readOnly={readOnly} close={() => { setEditing(null); void query.refetch(); }} />}
    <form className="project-filters" onSubmit={event => { event.preventDefault(); setSearch(draft.trim()); setPage(1); }}>
      <div className="field search-field"><label htmlFor="case-search">Buscar por nome ou chave</label><input id="case-search" maxLength={120} value={draft} onChange={e => setDraft(e.target.value)} /></div>
      <div className="field"><label htmlFor="case-filter">Status</label><select id="case-filter" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="all">Todos</option><option value="active">Ativos</option><option value="inactive">Inativos</option></select></div><button className="button">Buscar</button>
    </form>
    {query.isPending ? <p role="status">Carregando casos…</p> : query.isError ? <><ErrorNotice error={query.error} /><button className="button" onClick={() => void query.refetch()}>Tentar novamente</button></> : <>
      <p role="status">{query.data.total} casos encontrados</p>
      {!query.data.total && <p className="empty-projects">Nenhum caso neste filtro.</p>}
      <div className="project-grid">{query.data.items.map(item => <article className="project-card" key={item.id}>
        <span className="project-badge">{item.status === 'Active' ? 'Ativo' : 'Inativo'}</span><h2>{item.name}</h2><p><code>{item.stableKey}</code> · revisão {item.catalogVersion}</p><p className="project-description">{item.description || 'Sem descrição.'}</p><div className="suite-tags">{item.tags.map(tag => <span key={tag}>{tag}</span>)}</div>
        {!readOnly && <button className="button" onClick={() => setEditing(item)}>Editar {item.name}</button>}
      </article>)}</div>
      <div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button className="button" disabled={page * 12 >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div>
    </>}
  </>;
}

function CaseForm({ suiteId, projectId, existing, readOnly, close }: { suiteId: string; projectId: string; existing?: TestCase; readOnly: boolean; close: () => void }) {
  const [name, setName] = useState(existing?.name ?? '');
  const [stableKey, setKey] = useState(existing?.stableKey ?? '');
  const [description, setDescription] = useState(existing?.description ?? '');
  const [tags, setTags] = useState(existing?.tags.join(' ') ?? '');
  const [status, setStatus] = useState<SuiteStatus>(existing?.status ?? 'Active');
  const [error, setError] = useState<string | null>(null);
  const client = useQueryClient();
  const mutation = useMutation({ mutationFn: (input: CaseInput) => catalogApi.saveCase(suiteId, input, existing), onSuccess: async () => {
    await Promise.all([client.invalidateQueries({ queryKey: ['cases', suiteId] }), client.invalidateQueries({ queryKey: ['project', projectId] }), client.invalidateQueries({ queryKey: ['projects'] })]); close();
  } });
  function submit(event: FormEvent) {
    event.preventDefault(); if (readOnly || mutation.isPending) return;
    const input = { name, description, tags: parseTags(tags), status, stableKey: stableKey.trim().toLowerCase() };
    const validation = validateSuite(input) ?? (!/^[a-z0-9][a-z0-9_-]{0,79}$/.test(input.stableKey) ? 'Use uma chave com 1–80 letras sem acento, números, hífen ou sublinhado.' : null);
    setError(validation); if (!validation) mutation.mutate(input);
  }
  return <form className="project-form" onSubmit={submit} noValidate><h2>{existing ? 'Editar caso' : 'Novo caso'}</h2>
    <fieldset disabled={readOnly || mutation.isPending} className="catalog-fields">
      <div className="field"><label htmlFor="case-key">Chave permanente</label><input id="case-key" value={stableKey} onChange={e => setKey(e.target.value)} maxLength={80} disabled={!!existing} /><p className="field-help">Exemplo: login-valido. Não pode ser alterada após o cadastro.</p></div>
      <div className="field"><label htmlFor="case-name">Nome do caso</label><input id="case-name" value={name} onChange={e => setName(e.target.value)} maxLength={120} /></div>
      <div className="field"><label htmlFor="case-description">Descrição do caso</label><textarea id="case-description" value={description} onChange={e => setDescription(e.target.value)} maxLength={2000} rows={4} /></div>
      <div className="field"><label htmlFor="case-tags">Tags do caso</label><input id="case-tags" value={tags} onChange={e => setTags(e.target.value)} maxLength={1000} /></div>
      <div className="field"><label htmlFor="case-status">Status do caso</label><select id="case-status" value={status} onChange={e => setStatus(e.target.value as SuiteStatus)}><option value="Active">Ativo</option><option value="Inactive">Inativo</option></select></div>
      {error && <p role="alert" className="field-error">{error}</p>}{mutation.isError && <ErrorNotice error={mutation.error} />}
      <div className="form-actions"><button className="button primary">{mutation.isPending ? 'Salvando…' : 'Salvar caso'}</button><button className="button" type="button" onClick={close}>Cancelar / recarregar lista</button></div>
    </fieldset></form>;
}

export function EnvironmentCatalog() {
  const { projectId = '' } = useParams();
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId, signal) });
  const query = useQuery({ queryKey: ['environments', projectId], queryFn: ({ signal }) => catalogApi.environments(projectId, signal), refetchOnWindowFocus: false });
  const [editing, setEditing] = useState<ProjectEnvironment | 'new' | null>(null);
  const readOnly = !project.data || !!project.data.archivedAt;
  return <><Link className="back-link" to={`/projects/${projectId}`}>← Voltar ao projeto</Link>
    <div className="projects-title"><div className="page-title"><p className="eyebrow">CONFIGURAÇÃO DO PROJETO</p><h1>Ambientes</h1><p>{project.data?.name}</p></div>{!readOnly && query.data && query.data.length < 3 && <button className="button primary" onClick={() => setEditing('new')}>+ Novo ambiente</button>}</div>
    <p className="roadmap-note">Cadastre a URL base de cada ambiente. Production permanece desabilitado até a implementação de permissões.</p>
    {project.isError && <ErrorNotice error={project.error} />}{project.data?.archivedAt && <p className="status-message">Projeto arquivado: apenas consulta.</p>}
    {editing && <EnvironmentForm key={editing === 'new' ? 'new' : editing.version} projectId={projectId} existing={editing === 'new' ? undefined : editing} used={query.data?.map(x => x.name) ?? []} readOnly={readOnly} close={() => { setEditing(null); void query.refetch(); }} />}
    {query.isPending ? <p role="status">Carregando ambientes…</p> : query.isError ? <><ErrorNotice error={query.error} /><button className="button" onClick={() => void query.refetch()}>Tentar novamente</button></> : <div className="project-grid">
      {!query.data.length && <p className="empty-projects">Nenhum ambiente cadastrado.</p>}
      {query.data.map(item => <article className="project-card" key={item.id}><span className="project-badge">{item.enabled ? 'Habilitado' : 'Desabilitado'}</span><h2>{item.name}</h2><p className="project-description">{item.baseUrl}</p>{!readOnly && <button className="button" onClick={() => setEditing(item)}>Editar {item.name}</button>}</article>)}
    </div>}
  </>;
}

function EnvironmentForm({ projectId, existing, used, readOnly, close }: { projectId: string; existing?: ProjectEnvironment; used: EnvironmentName[]; readOnly: boolean; close: () => void }) {
  const names: EnvironmentName[] = ['Development', 'Staging', 'Production'];
  const [name, setName] = useState<EnvironmentName>(existing?.name ?? names.find(x => !used.includes(x)) ?? 'Development');
  const [baseUrl, setUrl] = useState(existing?.baseUrl ?? '');
  const [enabled, setEnabled] = useState(existing?.enabled ?? false);
  const [error, setError] = useState<string | null>(null);
  const client = useQueryClient();
  const mutation = useMutation({ mutationFn: (input: EnvironmentInput) => catalogApi.saveEnvironment(projectId, input, existing), onSuccess: async () => {
    await Promise.all([client.invalidateQueries({ queryKey: ['environments', projectId] }), client.invalidateQueries({ queryKey: ['project', projectId] }), client.invalidateQueries({ queryKey: ['projects'] })]); close();
  } });
  function submit(event: FormEvent) {
    event.preventDefault(); if (readOnly || mutation.isPending) return;
    const input = { name, baseUrl: baseUrl.trim(), enabled };
    const validation = validateEnvironment(input); setError(validation); if (!validation) mutation.mutate(input);
  }
  return <form className="project-form" onSubmit={submit} noValidate><h2>{existing ? 'Editar ambiente' : 'Novo ambiente'}</h2><fieldset className="catalog-fields" disabled={readOnly || mutation.isPending}>
    <div className="field"><label htmlFor="environment-name">Ambiente</label><select id="environment-name" disabled={!!existing} value={name} onChange={e => { setName(e.target.value as EnvironmentName); setEnabled(false); }}>{names.map(x => <option key={x} disabled={x !== existing?.name && used.includes(x)}>{x}</option>)}</select></div>
    <div className="field"><label htmlFor="environment-url">URL base</label><input id="environment-url" value={baseUrl} maxLength={2048} onChange={e => setUrl(e.target.value)} placeholder="https://staging.exemplo.com/" /><p className="field-help">HTTP(S), sem usuário, senha, parâmetros ou fragmentos.</p></div>
    <div className="field"><label htmlFor="environment-enabled">Disponibilidade</label><select id="environment-enabled" disabled={name === 'Production'} value={String(enabled)} onChange={e => setEnabled(e.target.value === 'true')}><option value="false">Desabilitado</option><option value="true">Habilitado</option></select></div>
    {error && <p className="field-error" role="alert">{error}</p>}{mutation.isError && <ErrorNotice error={mutation.error} />}
    <div className="form-actions"><button className="button primary">{mutation.isPending ? 'Salvando…' : 'Salvar ambiente'}</button><button className="button" type="button" onClick={close}>Cancelar / recarregar lista</button></div>
  </fieldset></form>;
}
