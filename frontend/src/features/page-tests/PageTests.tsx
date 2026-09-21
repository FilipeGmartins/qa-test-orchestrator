import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectsApi } from '../projects/api';
import { catalogApi } from '../catalog/api';
import { runApi, type TestRun } from '../runs/api';
import { useCanWrite } from '../auth/Auth';
import { apiRequest } from '../../api/http';
export const pageTestApi = { create: (projectId: string, input: { environmentId: string; url: string; checks: string[]; devices: string[] }) => apiRequest<TestRun>(`/projects/${projectId}/page-tests`, { method: 'POST', body: JSON.stringify(input) }) };
const checks = { load: 'Carregamento e recursos', console: 'Erros JavaScript e console', layout: 'Responsividade: rolagem horizontal' };
const devices = { desktop: 'Desktop · 1440 × 900', tablet: 'Tablet · 768 × 1024', mobile: 'Celular · 390 × 844' };
export function PageTests() {
  const canWrite = useCanWrite(); const navigate = useNavigate(); const client = useQueryClient();
  const [projectId, setProject] = useState(''); const [environmentId, setEnvironment] = useState(''); const [url, setUrl] = useState('');
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const [selectedChecks, setChecks] = useState(Object.keys(checks)); const [selectedDevices, setDevices] = useState(Object.keys(devices));
  const [review, setReview] = useState(false); const [error, setError] = useState('');
  const projects = useQuery({ queryKey: ['page-test-projects', search, page], queryFn: ({ signal }) => projectsApi.list(search, 'active', page, signal) });
  const environments = useQuery({ queryKey: ['environments', projectId], queryFn: ({ signal }) => catalogApi.environments(projectId, signal), enabled: !!projectId });
  const runner = useQuery({ queryKey: ['runner'], queryFn: ({ signal }) => runApi.capabilities(signal) });
  const mutation = useMutation({ mutationFn: () => pageTestApi.create(projectId, { environmentId, url: url.trim(), checks: selectedChecks, devices: selectedDevices }), onSuccess: async run => {
    client.setQueryData(['run', run.id], run); await Promise.all(['runs', 'suites', 'cases', 'projects', 'project'].map(key => client.invalidateQueries({ queryKey: [key] }))); navigate(`/test-runs/${run.id}`);
  } });
  if (!canWrite) return <><h1>Testar página por URL</h1><p role="alert">Seu perfil permite consultar resultados. Um Operador ou Administrador deve criar o teste.</p><Link to="/test-runs">Consultar execuções</Link></>;
  function submit(event: FormEvent) {
    event.preventDefault(); setError('');
    const environment = environments.data?.find(x => x.id === environmentId);
    try {
      const target = new URL(url.trim());
      if (!['http:', 'https:'].includes(target.protocol) || target.username || target.password || target.search || target.hash || url.length > 2000) throw new Error();
      if (!environment || !environment.enabled || environment.name === 'Production' || new URL(environment.baseUrl).origin !== target.origin) { setError('Selecione um ambiente habilitado da mesma origem da página.'); return; }
    } catch { setError('Informe uma URL HTTP/HTTPS sem credenciais, parâmetros ou fragmento.'); return; }
    if (!projectId || !selectedChecks.length || !selectedDevices.length) { setError('Selecione projeto, verificações e tamanhos de tela.'); return; }
    setReview(true);
  }
  function toggle(values: string[], value: string, set: (values: string[]) => void) { set(values.includes(value) ? values.filter(x => x !== value) : [...values, value]); }
  return <><div className="page-title"><p className="eyebrow">TESTES DE FRONTEND</p><h1>Testar página por URL</h1><p>Verifique carregamento, erros e rolagem horizontal, com capturas em diferentes tamanhos de tela.</p></div>
    <p className="status-message">Informe a URL final, sem redirecionamento HTTP. A origem deve estar aprovada pelo administrador do servidor. Recursos de outras origens também precisam de aprovação. Esta versão observa páginas públicas por 1,5 segundo após carregar o HTML; não preenche formulários nem testa fluxos de login ou compra.</p>
    {runner.data && !runner.data.enabled && <p role="status">Runner desabilitado. Você pode salvar a configuração; executar exige habilitar o worker.</p>}
    {[projects.error, environments.error, runner.error, mutation.error].filter(Boolean).map((e, i) => <p role="alert" key={i}>{e!.message}</p>)}
    {!review ? <form className="project-form page-audit-form" onSubmit={submit} noValidate><div className="field"><label htmlFor="page-project-search">Buscar projeto</label><input id="page-project-search" value={search} maxLength={120} onChange={e => { setSearch(e.target.value); setPage(1); }} /></div>
      <div className="field"><label htmlFor="page-project">Projeto</label><select id="page-project" value={projectId} onChange={e => { setProject(e.target.value); setEnvironment(''); }}><option value="">Selecione</option>{projectId && !projects.data?.items.some(p => p.id === projectId) && <option value={projectId}>Projeto selecionado</option>}{projects.data?.items.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
      <div className="pagination"><button type="button" className="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</button><span>Página {page}</span><button type="button" className="button" disabled={!projects.data || page * 12 >= projects.data.total} onClick={() => setPage(page + 1)}>Próxima</button></div>
      <div className="field"><label htmlFor="page-environment">Ambiente</label><select id="page-environment" value={environmentId} onChange={e => setEnvironment(e.target.value)}><option value="">Selecione</option>{environments.data?.map(e => <option key={e.id} value={e.id} disabled={!e.enabled || e.name === 'Production'}>{e.name} · {e.baseUrl}{!e.enabled && ' (desabilitado)'}</option>)}</select>{projectId && <Link to={`/projects/${projectId}/environments`}>Configurar ambientes</Link>}</div>
      <div className="field"><label htmlFor="page-url">URL da página</label><input id="page-url" type="url" placeholder="https://staging.exemplo.com/pagina" maxLength={2000} value={url} onChange={e => setUrl(e.target.value)} /></div>
      <fieldset><legend>Verificações</legend>{Object.entries(checks).map(([key,label]) => <label className="run-choice" key={key}><input type="checkbox" checked={selectedChecks.includes(key)} onChange={() => toggle(selectedChecks, key, setChecks)} />{label}</label>)}</fieldset>
      <fieldset><legend>Tamanhos de tela</legend>{Object.entries(devices).map(([key,label]) => <label className="run-choice" key={key}><input type="checkbox" checked={selectedDevices.includes(key)} onChange={() => toggle(selectedDevices, key, setDevices)} />{label}</label>)}</fieldset>
      {error && <p role="alert">{error}</p>}<button className="button primary" disabled={projects.isPending || environments.isFetching}>Revisar configuração</button></form> : <section className="project-form page-audit-review"><h2>Revise o teste de frontend</h2><p>{url.trim()}</p><p>{environments.data?.find(x => x.id === environmentId)?.name}</p><ul>{selectedChecks.map(x => <li key={x}>{checks[x as keyof typeof checks]}</li>)}</ul><ul>{selectedDevices.map(x => <li key={x}>{devices[x as keyof typeof devices]}</li>)}</ul><p>{selectedChecks.length * selectedDevices.length} verificações · Chromium · limite total de 120 segundos · captura por verificação.</p><p>A configuração reutiliza uma suíte “Frontend por URL” do projeto. Salvar cria uma execução pendente; confirme “Executar agora” nos detalhes para iniciar.</p><button className="button primary" disabled={mutation.isPending} onClick={() => mutation.mutate()}>{mutation.isPending ? 'Salvando…' : 'Salvar teste como pendente'}</button><button className="button" disabled={mutation.isPending} onClick={() => { mutation.reset(); setReview(false); }}>Voltar e editar</button></section>}
  </>;
}
