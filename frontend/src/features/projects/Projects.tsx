import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, projectsApi, validateProject, type Project, type ProjectInput, type ProjectStatus } from './api';

const date = (value: string) => new Date(value).toLocaleString('pt-BR', { dateStyle: 'medium', timeStyle: 'short' });

function Heading({ title, description }: { title: string; description: string }) {
  return <div className="page-title"><p className="eyebrow">ORGANIZAÇÃO DOS TESTES</p><h1>{title}</h1><p>{description}</p></div>;
}

function RequestError({ error, retry }: { error: Error; retry?: () => void }) {
  return <div className="error-message" role="alert"><strong>{error.message}</strong>{retry && <div className="form-actions"><button className="button" onClick={retry}>Tentar novamente</button></div>}</div>;
}

export function ProjectList() {
  const [params, setParams] = useSearchParams();
  const search = params.get('search') ?? '';
  const rawStatus = params.get('status');
  const status: ProjectStatus = rawStatus === 'archived' || rawStatus === 'all' ? rawStatus : 'active';
  const pageValue = Number(params.get('page') ?? '1');
  const page = Number.isInteger(pageValue) && pageValue > 0 && pageValue <= 100000 ? pageValue : 1;
  const [draft, setDraft] = useState(search);
  useEffect(() => setDraft(search), [search]);
  const query = useQuery({ queryKey: ['projects', { search, status, page }], queryFn: ({ signal }) => projectsApi.list(search, status, page, signal) });
  function change(next: { search?: string; status?: ProjectStatus; page?: number }) {
    setParams({ search: next.search ?? search, status: next.status ?? status, page: String(next.page ?? 1) });
  }
  function filter(event: FormEvent) { event.preventDefault(); change({ search: draft.trim() }); }
  return <>
    <div className="projects-title"><Heading title="Projetos" description="Organize os produtos e aplicações que sua equipe vai testar." /><Link className="button primary" to="/projects/new">+ Novo projeto</Link></div>
    <form className="project-filters" onSubmit={filter}>
      <div className="field search-field"><label htmlFor="project-search">Buscar por nome</label><input id="project-search" type="search" placeholder="Nome do projeto" maxLength={120} value={draft} onChange={event => setDraft(event.target.value)} /></div>
      <div className="field"><label htmlFor="project-status">Status</label><select id="project-status" value={status} onChange={event => change({ status: event.target.value as ProjectStatus })}><option value="active">Ativos</option><option value="archived">Arquivados</option><option value="all">Todos</option></select></div>
      <button className="button" type="submit">Buscar</button>
    </form>
    {query.isPending ? <p className="status-message" role="status">Carregando projetos…</p> : query.isError ? <RequestError error={query.error} retry={() => void query.refetch()} /> : <>
      <p className="result-count" role="status">{query.data.total} {query.data.total === 1 ? 'projeto encontrado' : 'projetos encontrados'}</p>
      {query.data.items.length === 0 ? <section className="empty-projects"><span className="empty-symbol" aria-hidden="true">▱</span><h2>{search || status !== 'active' || page > 1 ? 'Nenhum projeto neste filtro' : 'Seu primeiro projeto começa aqui'}</h2><p>{search || status !== 'active' || page > 1 ? 'Ajuste a busca, o status ou volte à primeira página.' : 'Cadastre uma aplicação para começar a organizar seu catálogo de testes.'}</p>{search || status !== 'active' || page > 1 ? <button className="button" onClick={() => { setDraft(''); setParams({}); }}>Limpar filtros</button> : <Link className="button primary" to="/projects/new">Criar primeiro projeto</Link>}</section> : <div className="project-grid">{query.data.items.map(project => <article className="project-card" key={project.id}><div className="project-card-top"><span className="project-monogram" aria-hidden="true">{project.name.slice(0, 2).toLocaleUpperCase('pt-BR')}</span><ProjectBadge project={project} /></div><h2><Link to={`/projects/${project.id}`}>{project.name}</Link></h2><p className="project-description">{project.description || 'Sem descrição.'}</p><div className="project-card-bottom"><span>Atualizado em {date(project.updatedAt)}</span><Link aria-label={`Abrir projeto ${project.name}`} to={`/projects/${project.id}`}>Abrir →</Link></div></article>)}</div>}
      <div className="pagination"><button className="button" disabled={page === 1} onClick={() => change({ page: page - 1 })}>Anterior</button><span>Página {page} de {Math.max(1, Math.ceil(query.data.total / 12))}</span><button className="button" disabled={page * 12 >= query.data.total} onClick={() => change({ page: page + 1 })}>Próxima</button></div>
    </>}
  </>;
}

function ProjectBadge({ project }: { project: Project }) {
  return <span className={project.archivedAt ? 'project-badge archived' : 'project-badge'}>{project.archivedAt ? 'Arquivado' : 'Ativo'}</span>;
}

export function ProjectDetails() {
  const { id = '' } = useParams();
  const client = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  const query = useQuery({ queryKey: ['project', id], queryFn: ({ signal }) => projectsApi.get(id, signal) });
  const archive = useMutation({
    mutationFn: (project: Project) => projectsApi.archive(project.id, project.version),
    onSuccess: async project => {
      client.setQueryData(['project', id], project);
      await client.invalidateQueries({ queryKey: ['projects'] });
      setConfirming(false);
    },
  });
  if (query.isPending) return <p className="status-message" role="status">Carregando projeto…</p>;
  if (query.isError) return <><Link className="back-link" to="/projects">← Projetos</Link><RequestError error={query.error} retry={() => void query.refetch()} /></>;
  const project = query.data;
  return <>
    <Link className="back-link" to="/projects">← Todos os projetos</Link>
    <Link className="button" to={`/projects/${id}/presets`}>Presets</Link>
    <Link className="button suite-link" to={`/projects/${id}/test-suites`}>Ver suítes de teste</Link><Link className="button" to={`/projects/${id}/environments`}>Ambientes</Link><Link className="button" to={`/projects/${id}/test-runs`}>Histórico de execuções</Link>
    <div className="projects-title"><Heading title={project.name} description="Informações e organização do projeto." /><ProjectBadge project={project} /></div>
    {archive.isSuccess && <p className="success-message" role="status">Projeto arquivado. As informações foram preservadas.</p>}
    <section className="project-detail"><h2>Sobre o projeto</h2><p className="description-full">{project.description || 'Nenhuma descrição adicionada.'}</p><dl><div><dt>Criado em</dt><dd>{date(project.createdAt)}</dd></div><div><dt>Atualizado em</dt><dd>{date(project.updatedAt)}</dd></div>{project.archivedAt && <div><dt>Arquivado em</dt><dd>{date(project.archivedAt)}</dd></div>}<div><dt>Identificador</dt><dd className="project-id">{project.id}</dd></div></dl></section>
    {!project.archivedAt ? <div className="form-actions"><Link className="button primary" to={`/projects/${id}/edit`}>Editar projeto</Link><button className="button danger" onClick={() => { archive.reset(); setConfirming(true); }}>Arquivar projeto</button></div> : <p className="status-message">Este projeto está arquivado e disponível apenas para consulta.</p>}
    {confirming && !project.archivedAt && <section className="archive-confirmation" aria-labelledby="archive-title"><h2 id="archive-title">Arquivar “{project.name}”?</h2><p>O projeto sairá da lista de ativos. Seus dados serão preservados e poderão ser consultados no filtro Arquivados.</p>{archive.isError && <RequestError error={archive.error} />}{archive.error instanceof ApiError && archive.error.code === 'CONCURRENT_UPDATE' && <button className="button" onClick={() => { void query.refetch(); archive.reset(); setConfirming(false); }}>Recarregar projeto</button>}<div className="form-actions"><button className="button danger" disabled={archive.isPending} onClick={() => archive.mutate(project)}>{archive.isPending ? 'Arquivando…' : 'Confirmar arquivamento'}</button><button className="button" disabled={archive.isPending} onClick={() => setConfirming(false)}>Manter ativo</button></div></section>}
    <p className="roadmap-note">Organize o catálogo em suítes de teste. Casos de teste serão adicionados na próxima entrega.</p>
  </>;
}

export function ProjectEditor() {
  const { id } = useParams();
  const query = useQuery({ queryKey: ['project', id], queryFn: ({ signal }) => projectsApi.get(id!, signal), enabled: Boolean(id), refetchOnWindowFocus: false });
  if (id && query.isPending) return <p className="status-message" role="status">Carregando projeto…</p>;
  if (id && query.isError) return <RequestError error={query.error} retry={() => void query.refetch()} />;
  if (query.data?.archivedAt) return <><p className="status-message">Projetos arquivados não podem ser editados.</p><Link className="button" to={`/projects/${id}`}>Voltar ao projeto</Link></>;
  return <><Link className="back-link" to={id ? `/projects/${id}` : '/projects'}>← Voltar</Link><Heading title={id ? 'Editar projeto' : 'Novo projeto'} description="Dê um nome claro e descreva o que será testado." /><ProjectForm key={query.data?.version ?? 'new'} project={query.data} onReload={() => void query.refetch()} /></>;
}

export function ProjectForm({ project, onReload }: { project?: Project; onReload?: () => void }) {
  const [input, setInput] = useState<ProjectInput>({ name: project?.name ?? '', description: project?.description ?? '' });
  const [errors, setErrors] = useState<Partial<Record<keyof ProjectInput, string>>>({});
  const client = useQueryClient();
  const navigate = useNavigate();
  const mutation = useMutation({
    mutationFn: (values: ProjectInput) => project ? projectsApi.update(project.id, values, project.version) : projectsApi.create(values),
    onSuccess: async saved => {
      client.setQueryData(['project', saved.id], saved);
      await client.invalidateQueries({ queryKey: ['projects'] });
      navigate(`/projects/${saved.id}`);
    },
    onError: error => {
      if (error instanceof ApiError && (error.field === 'name' || error.field === 'description')) setErrors({ [error.field]: error.message });
    },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    if (mutation.isPending) return;
    const validation = validateProject(input);
    setErrors(validation);
    if (Object.keys(validation).length) return;
    mutation.mutate({ name: input.name.trim(), description: input.description.trim() });
  }
  return <form className="project-form" onSubmit={submit} noValidate>
    <div className="field"><label htmlFor="project-name">Nome do projeto <span aria-hidden="true">*</span></label><input id="project-name" required maxLength={120} autoFocus value={input.name} disabled={mutation.isPending} aria-invalid={Boolean(errors.name)} aria-describedby="name-help name-error" onChange={event => setInput({ ...input, name: event.target.value })} /><p id="name-help" className="field-help">Use até 120 caracteres. Exemplo: Portal do cliente.</p><p id="name-error" className="field-error">{errors.name}</p></div>
    <div className="field"><label htmlFor="project-description">Descrição <span className="optional">opcional</span></label><textarea id="project-description" maxLength={2000} rows={6} value={input.description} disabled={mutation.isPending} aria-invalid={Boolean(errors.description)} aria-describedby="description-help description-error" onChange={event => setInput({ ...input, description: event.target.value })} /><p id="description-help" className="field-help">Contexto, objetivo e escopo dos testes. {input.description.length}/2000</p><p id="description-error" className="field-error">{errors.description}</p></div>
    {mutation.isError && <RequestError error={mutation.error} />}
    {mutation.error instanceof ApiError && mutation.error.code === 'CONCURRENT_UPDATE' && <button className="button" type="button" onClick={onReload}>Descartar edição e carregar versão atual</button>}
    <div className="form-actions"><button type="submit" className="button primary" disabled={mutation.isPending}>{mutation.isPending ? 'Salvando…' : project ? 'Salvar alterações' : 'Criar projeto'}</button><Link className="button" to={project ? `/projects/${project.id}` : '/projects'}>Cancelar</Link></div>
  </form>;
}
