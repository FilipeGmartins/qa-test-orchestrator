import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { projectsApi, type Project } from '../projects/api';
import { ApiError } from '../../api/http';
import { suiteApi, parseTags, validateSuite, type Suite, type SuiteInput, type SuiteStatus } from './api';

function ErrorNotice({ error, retry }: { error: Error; retry?: () => void }) {
  return <div className="error-message" role="alert"><p>{error.message}</p>{retry && <button className="button" onClick={retry}>Tentar novamente</button>}</div>;
}
function Title({ title, description }: { title: string; description: string }) {
  return <div className="page-title"><p className="eyebrow">CATÁLOGO DE TESTES</p><h1>{title}</h1><p>{description}</p></div>;
}

export function SuiteProjectPicker() {
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const query = useQuery({ queryKey: ['projects', 'suite-picker', search, page], queryFn: ({ signal }) => projectsApi.list(search, 'all', page, signal) });
  return <><Title title="Suítes de teste" description="Escolha um projeto para organizar suas suítes." /><form className="project-filters" onSubmit={event => { event.preventDefault(); setSearch(draft.trim()); setPage(1); }}><div className="field search-field"><label htmlFor="suite-project-search">Buscar projeto</label><input id="suite-project-search" maxLength={120} value={draft} onChange={event => setDraft(event.target.value)} /></div><button className="button">Buscar</button></form>{query.isPending ? <p role="status">Carregando projetos…</p> : query.isError ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : <>{!query.data.items.length && <section className="empty-projects"><h2>Nenhum projeto encontrado</h2><p>Cadastre um projeto ou ajuste sua busca.</p><Link className="button primary" to="/projects/new">Novo projeto</Link></section>}<div className="project-grid">{query.data.items.map(project => <article className="project-card" key={project.id}><span className="project-badge">{project.archivedAt ? 'Arquivado · consulta' : 'Ativo'}</span><h2>{project.name}</h2><p className="project-description">{project.description || 'Sem descrição.'}</p><Link className="button" to={`/projects/${project.id}/test-suites`}>Ver suítes de {project.name}</Link></article>)}</div><div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button className="button" disabled={page * 12 >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div></>}</>;
}

export function SuiteList() {
  const { projectId = '' } = useParams();
  const [search, setSearch] = useState('');
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId, signal) });
  const query = useQuery({ queryKey: ['suites', projectId, search, status, page], queryFn: ({ signal }) => suiteApi.list(projectId, search, status, page, signal) });
  return <><Link className="back-link" to={`/projects/${projectId}`}>← Voltar ao projeto</Link><div className="projects-title"><Title title="Suítes de teste" description={project.data?.name ?? 'Organize testes por objetivo ou funcionalidade.'} />{project.data && !project.data.archivedAt && <Link className="button primary" to={`/projects/${projectId}/test-suites/new`}>+ Nova suíte</Link>}</div>{project.isError && <ErrorNotice error={project.error} retry={() => void project.refetch()} />}{project.data?.archivedAt && <p className="status-message">Projeto arquivado. Suítes disponíveis apenas para consulta.</p>}<form className="project-filters" onSubmit={event => { event.preventDefault(); setSearch(draft.trim()); setPage(1); }}><div className="field search-field"><label htmlFor="suite-search">Buscar suíte</label><input id="suite-search" maxLength={120} value={draft} onChange={event => setDraft(event.target.value)} /></div><div className="field"><label htmlFor="suite-filter">Status</label><select id="suite-filter" value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}><option value="all">Todos</option><option value="active">Ativas</option><option value="inactive">Inativas</option></select></div><button className="button">Buscar</button></form>{query.isPending ? <p role="status">Carregando suítes…</p> : query.isError ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : <><p className="result-count" role="status">{query.data.total} suítes encontradas</p>{!query.data.items.length && <section className="empty-projects"><h2>Nenhuma suíte neste filtro</h2><p>Crie uma suíte ou ajuste a busca e o status.</p></section>}<div className="project-grid">{query.data.items.map(suite => <article className="project-card" key={suite.id}><span className={suite.status === 'Active' ? 'project-badge' : 'project-badge archived'}>{suite.status === 'Active' ? 'Ativa' : 'Inativa'}</span><h2>{suite.name}</h2><p className="project-description">{suite.description || 'Sem descrição.'}</p><div className="suite-tags">{suite.tags.map(tag => <span key={tag}>{tag}</span>)}</div><Link className="button" to={`/test-suites/${suite.id}`}>Abrir suíte {suite.name}</Link></article>)}</div><div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page} de {Math.max(1, Math.ceil(query.data.total / 12))}</span><button className="button" disabled={page * 12 >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div></>}<p className="roadmap-note">Abra uma suíte para consultar e organizar seus casos de teste.</p></>;
}

export function SuiteEditor() {
  const { id, projectId: routeProjectId } = useParams();
  const suite = useQuery({ queryKey: ['suite', id], queryFn: ({ signal }) => suiteApi.get(id!, signal), enabled: Boolean(id), refetchOnWindowFocus: false });
  const projectId = suite.data?.projectId ?? routeProjectId;
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId!, signal), enabled: Boolean(projectId), refetchOnWindowFocus: false });
  if (suite.isError || project.isError) return <ErrorNotice error={suite.error ?? project.error!} retry={() => { if (id) void suite.refetch(); if (projectId) void project.refetch(); }} />;
  if ((id && suite.isPending) || project.isPending) return <p role="status">Carregando suíte e projeto…</p>;
  return <><Link className="back-link" to={`/projects/${projectId}/test-suites`}>← Voltar às suítes</Link><Title title={id ? 'Detalhes da suíte' : 'Nova suíte'} description={project.data!.name} /><>{id && <Link className="button" to={`/test-suites/${id}/test-cases`}>Ver casos de teste</Link>}</><SuiteForm key={suite.data?.version ?? 'new'} project={project.data!} suite={suite.data} reload={() => { void suite.refetch(); void project.refetch(); }} /></>;
}

export function SuiteForm({ project, suite, reload }: { project: Project; suite?: Suite; reload: () => void }) {
  const [name, setName] = useState(suite?.name ?? '');
  const [description, setDescription] = useState(suite?.description ?? '');
  const [tags, setTags] = useState(suite?.tags.join(' ') ?? '');
  const [status, setStatus] = useState<SuiteStatus>(suite?.status ?? 'Active');
  const [validation, setValidation] = useState<string | null>(null);
  const client = useQueryClient();
  const navigate = useNavigate();
  const mutation = useMutation({ mutationFn: (input: SuiteInput) => suite ? suiteApi.update(suite, input) : suiteApi.create(project.id, input), onSuccess: async saved => {
    client.setQueryData(['suite', saved.id], saved);
    await Promise.all([client.invalidateQueries({ queryKey: ['suites', project.id] }), client.invalidateQueries({ queryKey: ['project', project.id] }), client.invalidateQueries({ queryKey: ['projects'] })]);
    navigate(`/projects/${project.id}/test-suites`);
  } });
  const disabled = Boolean(project.archivedAt) || mutation.isPending;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (disabled) return;
    const input = { name: name.trim(), description: description.trim(), tags: parseTags(tags), status };
    const error = validateSuite(input);
    setValidation(error);
    if (!error) mutation.mutate(input);
  }
  return <form className="project-form" onSubmit={submit} noValidate>{project.archivedAt && <p className="status-message">Projeto arquivado: apenas consulta.</p>}<div className="field"><label htmlFor="suite-name">Nome da suíte</label><input id="suite-name" value={name} onChange={event => setName(event.target.value)} maxLength={120} required disabled={disabled} /></div><div className="field"><label htmlFor="suite-description">Descrição</label><textarea id="suite-description" value={description} onChange={event => setDescription(event.target.value)} maxLength={2000} rows={4} disabled={disabled} /></div><div className="field"><label htmlFor="suite-tags">Tags</label><input id="suite-tags" value={tags} onChange={event => setTags(event.target.value)} maxLength={1000} aria-describedby="suite-tags-help" disabled={disabled} /><p className="field-help" id="suite-tags-help">Até 20 tags separadas por espaço ou vírgula. Exemplo: @smoke @login</p></div><div className="field"><label htmlFor="suite-status">Status da suíte</label><select id="suite-status" value={status} onChange={event => setStatus(event.target.value as SuiteStatus)} disabled={disabled}><option value="Active">Ativa</option><option value="Inactive">Inativa</option></select></div>{validation && <p className="field-error" role="alert">{validation}</p>}{mutation.isError && <ErrorNotice error={mutation.error} />}{mutation.error instanceof ApiError && mutation.error.code === 'CONCURRENT_UPDATE' && <button className="button" type="button" onClick={reload}>Descartar edição e recarregar</button>}<div className="form-actions">{!project.archivedAt && <button className="button primary" disabled={disabled}>{mutation.isPending ? 'Salvando…' : suite ? 'Salvar suíte' : 'Criar suíte'}</button>}<Link className="button" to={`/projects/${project.id}/test-suites`}>Voltar</Link></div></form>;
}
