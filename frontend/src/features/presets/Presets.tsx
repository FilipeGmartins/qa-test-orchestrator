import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { presetApi } from './api';
import { projectsApi } from '../projects/api';
import { Snapshot } from '../runs/Runs';

function Pages({ page, total, setPage }: { page: number; total: number; setPage: (value: number) => void }) { return <div className="pagination"><button className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button className="button" disabled={page * 12 >= total} onClick={() => setPage(page + 1)}>Próxima</button></div>; }
export function PresetProjects() {
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['preset-projects', search, page], queryFn: ({ signal }) => projectsApi.list(search, 'all', page, signal) });
  return <><div className="page-title"><h1>Presets por projeto</h1><p>Escolha um projeto para salvar e reutilizar configurações.</p></div><div className="field"><label htmlFor="preset-project-search">Buscar projeto</label><input id="preset-project-search" maxLength={120} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></div>
    {query.isPending ? <p>Carregando projetos…</p> : query.isError ? <p role="alert">{query.error.message}<button className="button" onClick={() => void query.refetch()}>Recarregar</button></p> : <><div className="project-grid">{query.data.items.map(p => <article className="project-card" key={p.id}><h2>{p.name}</h2>{p.archivedAt && <p>Arquivado</p>}<Link className="button" to={`/projects/${p.id}/presets`}>Ver presets</Link></article>)}</div>{!query.data.total && <p>Nenhum projeto encontrado.</p>}<Pages page={page} total={query.data.total} setPage={setPage} /></>}
  </>;
}
export function PresetList() {
  const { projectId = '' } = useParams(); const [search, setSearch] = useState(''); const [status, setStatus] = useState('active'); const [page, setPage] = useState(1);
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => projectsApi.get(projectId, signal) });
  const query = useQuery({ queryKey: ['presets', projectId, search, status, page], queryFn: ({ signal }) => presetApi.list(projectId, search, status, page, signal) });
  return <><Link className="back-link" to={`/projects/${projectId}`}>← Voltar ao projeto</Link><div className="projects-title"><div className="page-title"><h1>Presets</h1><p>{project.data?.name} · Configurações reutilizáveis, com revisões preservadas.</p></div>{project.data && !project.data.archivedAt && <Link className="button primary" to={`/projects/${projectId}/presets/new`}>Novo preset</Link>}</div>
    {project.isError && <p role="alert">{project.error.message}</p>}{project.data?.archivedAt && <p>Projeto arquivado: apenas consulta.</p>}
    <div className="result-filters"><div className="field"><label htmlFor="preset-search">Buscar preset</label><input id="preset-search" maxLength={120} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></div><div className="field"><label htmlFor="preset-status">Status do preset</label><select id="preset-status" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="active">Ativos</option><option value="archived">Arquivados</option><option value="all">Todos</option></select></div></div>
    {query.isPending ? <p>Carregando presets…</p> : query.isError ? <p role="alert">{query.error.message}<button className="button" onClick={() => void query.refetch()}>Recarregar presets</button></p> : <><p>{query.data.total} presets encontrados</p><div className="project-grid">{query.data.items.map(p => <article className="project-card" key={p.id}><span className="project-badge">{p.archived ? 'Arquivado' : 'Ativo'} · revisão {p.revision}</span><h2>{p.name}</h2><p>{p.description}</p><p>{p.snapshot.suiteName} · {p.snapshot.environmentName} · {p.configuration.options.browser}</p><Link className="button" to={`/presets/${p.id}`}>Ver preset</Link></article>)}</div><Pages page={page} total={query.data.total} setPage={setPage} /></>}
  </>;
}
export function PresetDetails() {
  const { id = '' } = useParams(); const client = useQueryClient(); const navigate = useNavigate(); const [confirmArchive, setConfirmArchive] = useState(false); const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['preset', id], queryFn: ({ signal }) => presetApi.get(id, signal) });
  const project = useQuery({ queryKey: ['project', query.data?.projectId], queryFn: ({ signal }) => projectsApi.get(query.data!.projectId, signal), enabled: !!query.data });
  const revisions = useQuery({ queryKey: ['preset-revisions', id, page], queryFn: ({ signal }) => presetApi.revisions(id, page, signal) });
  const preview = useMutation({ mutationFn: () => presetApi.preview(id) });
  const usePreset = useMutation({ mutationFn: () => presetApi.use(id, preview.data!), onSuccess: async run => { client.setQueryData(['run', run.id], run); await Promise.all([client.invalidateQueries({ queryKey: ['presets'] }), client.invalidateQueries({ queryKey: ['preset', id] }), client.invalidateQueries({ queryKey: ['runs'] }), client.invalidateQueries({ queryKey: ['projects'] }), client.invalidateQueries({ queryKey: ['project', run.projectId] })]); navigate(`/test-runs/${run.id}`); } });
  const archive = useMutation({ mutationFn: () => presetApi.archive(query.data!), onSuccess: async value => { client.setQueryData(['preset', id], value); await client.invalidateQueries({ queryKey: ['presets'] }); preview.reset(); setConfirmArchive(false); } });
  if (query.isPending) return <p>Carregando preset…</p>;
  if (query.isError) return <p role="alert">{query.error.message}<button className="button" onClick={() => void query.refetch()}>Recarregar preset</button></p>;
  const preset = query.data; const writable = project.data && !project.data.archivedAt && !preset.archived;
  return <><Link className="back-link" to={`/projects/${preset.projectId}/presets`}>← Voltar aos presets</Link><div className="page-title"><h1>{preset.name}</h1><p>{preset.description}</p><p>{preset.archived ? 'Arquivado' : 'Ativo'} · revisão {preset.revision}</p></div>
    {[project.error, preview.error, usePreset.error, archive.error].filter(Boolean).map((e, i) => <p role="alert" className="error-message" key={i}>{e!.message}</p>)}
    {writable && <div className="form-actions"><button className="button primary" disabled={preview.isPending || usePreset.isPending} onClick={() => { usePreset.reset(); preview.mutate(); }}>Revisar para usar</button><Link className="button" to={`/projects/${preset.projectId}/presets/${id}/edit`}>Editar preset</Link><button className="button" onClick={() => setConfirmArchive(true)}>Arquivar preset</button></div>}
    {confirmArchive && writable && <div className="form-actions"><p>Arquivar este preset? As revisões e execuções serão preservadas.</p><button className="button danger" disabled={archive.isPending} onClick={() => archive.mutate()}>Confirmar arquivamento</button><button className="button" onClick={() => setConfirmArchive(false)}>Voltar</button></div>}
    {preview.data && writable && <section className="project-form"><h2>Revisão antes de reutilizar</h2><p>{preview.data.name} · revisão {preview.data.revision}</p><p>Os dados abaixo foram revalidados no catálogo atual. Confirme para criar uma execução pendente; isso não inicia os testes.</p><Snapshot configuration={preview.data.configuration} /><button className="button primary" disabled={usePreset.isPending || preview.isPending || usePreset.isError} onClick={() => usePreset.mutate()}>Criar execução pendente</button><button className="button" onClick={() => preview.reset()}>Fechar revisão</button>{usePreset.isError && <p>Recarregue o preset e use “Revisar para usar” novamente.</p>}</section>}
    {(archive.isError || usePreset.isError) && <button className="button" onClick={() => { archive.reset(); usePreset.reset(); preview.reset(); void query.refetch(); }}>Recarregar preset</button>}
    <Snapshot configuration={preset.snapshot} />
    <section className="project-form"><h2>Histórico de revisões</h2>{revisions.isPending ? <p>Carregando revisões…</p> : revisions.isError ? <p role="alert">{revisions.error.message}<button className="button" onClick={() => void revisions.refetch()}>Recarregar revisões</button></p> : <>{revisions.data.items.map(r => <details key={r.revision}><summary>Revisão {r.revision} · {r.name} · {new Date(r.createdAt).toLocaleString('pt-BR')}</summary><p>{r.description}</p><Snapshot configuration={r.snapshot} /></details>)}<Pages page={page} total={revisions.data.total} setPage={setPage} /></>}</section>
  </>;
}
