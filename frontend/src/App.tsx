import { NavLink, Link, Route, Routes } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getSystemStatus } from './api/system';
import { ProjectList, ProjectDetails, ProjectEditor } from './features/projects/Projects';
import { SuiteProjectPicker, SuiteList, SuiteEditor } from './features/suites/Suites';

import { CaseCatalog, EnvironmentCatalog } from './features/catalog/Catalog';

import { RunHistory, RunDetails, RunWizard } from './features/runs/Runs';
import { CaseResults } from './features/runs/Results';

const roadmap = [
  ['01', 'Fundação', 'Estrutura e conectividade', 'Em validação'],
  ['02', 'Projetos', 'Cadastro e organização', 'Disponível'],
  ['03', 'Suítes de teste', 'Suítes, casos e ambientes', 'Disponível'],
  ['04', 'Execuções', 'Configuração e histórico', 'Disponível'],
  ['05', 'Runner Playwright', 'Catálogo aprovado e fila', 'Disponível'],
  ['06', 'Resultados e evidências', 'Falhas, arquivos e histórico por caso', 'Disponível'],
];

export function App() {
  return <div className="app-shell">
    <a className="skip-link" href="#main">Pular para o conteúdo</a>
    <aside className="sidebar">
      <Link to="/" className="brand"><span className="brand-mark">Q</span><span>QA Orchestrator<small>TEST OPERATIONS</small></span></Link>
      <p className="nav-label">WORKSPACE</p>
      <nav aria-label="Navegação principal">
        <NavLink to="/" end><span aria-hidden="true">▦</span> Visão geral</NavLink>
        <NavLink to="/projects"><span aria-hidden="true">▱</span> Projetos</NavLink>
        <NavLink to="/test-suites"><span aria-hidden="true">≡</span> Suítes de teste</NavLink>
        <NavLink to="/test-runs"><span aria-hidden="true">▷</span> Execuções</NavLink>
        <NavLink to="/settings"><span aria-hidden="true">◎</span> Status do sistema</NavLink>
      </nav>
      <div className="future-nav"><p className="nav-label">PRÓXIMAS ENTREGAS</p><span>Dashboard</span><span>Presets</span><span>Autenticação</span></div>
      <div className="sidebar-footer"><span className="environment-dot" /> Ambiente local<small>Resultados · v0.7.0</small></div>
    </aside>
    <div className="workspace">
      <header className="topbar"><span>Workspace <span className="separator">/</span> QA Test Orchestrator</span><span className="phase-badge">FASE 06</span></header>
      <main id="main" tabIndex={-1}>
        <Routes>
          <Route path="/" element={<Overview />} />
          <Route path="/projects" element={<ProjectList />} />
          <Route path="/projects/new" element={<ProjectEditor />} />
          <Route path="/projects/:id" element={<ProjectDetails />} />
          <Route path="/projects/:id/edit" element={<ProjectEditor />} />
          <Route path="/test-suites" element={<SuiteProjectPicker />} />
          <Route path="/projects/:projectId/test-suites" element={<SuiteList />} />
          <Route path="/projects/:projectId/test-suites/new" element={<SuiteEditor />} />
          <Route path="/test-suites/:id" element={<SuiteEditor />} />
          <Route path="/test-suites/:suiteId/test-cases" element={<CaseCatalog />} />
          <Route path="/projects/:projectId/environments" element={<EnvironmentCatalog />} />
          <Route path="/test-runs" element={<RunHistory />} />
          <Route path="/projects/:projectId/test-runs" element={<RunHistory />} />
          <Route path="/projects/:projectId/test-runs/new" element={<RunWizard />} />
          <Route path="/test-runs/:id" element={<RunDetails />} />
          <Route path="/test-cases/:id/results" element={<CaseResults />} />
          <Route path="/settings" element={<><PageTitle eyebrow="DIAGNÓSTICO" title="Status do sistema" description="Acompanhe a conexão entre a interface, a API e o banco de dados." /><SystemHealth /></>} />
          <Route path="*" element={<><PageTitle eyebrow="404" title="Página não encontrada" description="Este endereço não está disponível." /><Link className="button" to="/">Voltar ao início</Link></>} />
        </Routes>
      </main>
      <footer className="page-footer">QA Test Orchestrator <span>Construído para dar clareza aos testes.</span></footer>
    </div>
  </div>;
}

function PageTitle({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return <div className="page-title"><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{description}</p></div>;
}

function Overview() {
  return <>
    <PageTitle eyebrow="VISÃO GERAL" title="Uma base para testar melhor." description="Seu espaço para organizar testes, acompanhar execuções e investigar resultados." />
    <section className="intro-panel" aria-labelledby="foundation-title">
      <div><span className="intro-tag">CATÁLOGO DE TESTES</span><h2 id="foundation-title">Organize o catálogo.<br className="desktop-break" /> Prepare suas execuções.</h2><p>Projetos, suítes, casos e ambientes já podem ser cadastrados. Acompanhe as próximas entregas no roadmap.</p><a href="#services" className="intro-link">Verificar serviços <span aria-hidden="true">↓</span></a></div>
      <div className="flow-diagram" aria-label="Fluxo: interface React, API ASP.NET Core e banco PostgreSQL"><div><span>01</span>Interface <small>React + TypeScript</small></div><b aria-hidden="true">↓</b><div><span>02</span>API <small>ASP.NET Core</small></div><b aria-hidden="true">↓</b><div><span>03</span>Banco de dados <small>PostgreSQL</small></div></div>
    </section>
    <SystemHealth />
    <section className="roadmap" aria-labelledby="roadmap-title"><div className="section-heading"><div><h2 id="roadmap-title">O caminho até a primeira execução</h2><p>Entregas pequenas, com validação em cada etapa.</p></div><a className="intro-link" href="https://github.com/users/FilipeGmartins/projects/1" target="_blank" rel="noreferrer">Acompanhar no GitHub ↗</a></div><ol>{roadmap.map(([number, name, detail, state]) => <li key={number}><span className="step-number">{number}</span><div><h3>{name}</h3><p>{detail}</p></div><span className={number === '01' ? 'step-state current' : 'step-state'}>{state}</span></li>)}</ol><p className="roadmap-note">O runner executa casos do catálogo aprovado. Salve a configuração e confirme a execução nos detalhes; o worker deve estar habilitado no servidor.</p></section>
  </>;
}

export function SystemHealth() {
  const query = useQuery({ queryKey: ['system'], queryFn: ({ signal }) => getSystemStatus(signal), refetchInterval: 15_000 });
  return <section id="services" className="services" aria-labelledby="services-title">
    <div className="section-heading"><div><h2 id="services-title">Conectividade da plataforma</h2><p>Atualização automática a cada 15 segundos.</p></div><button className="button" onClick={() => void query.refetch()} disabled={query.isFetching}>{query.isFetching ? 'Verificando…' : 'Verificar agora'}</button></div>
    <div aria-live="polite">
      {query.isPending ? <p className="status-message" role="status">Verificando conexão com os serviços…</p> : query.isError ? <div className="error-message" role="alert"><strong>API indisponível</strong><p>Não foi possível obter um diagnóstico atualizado. Verifique se o backend está em execução e tente novamente.</p></div> : <>
        <div className="service-grid">
          <Service name="Interface" description="Aplicação carregada no navegador" available />
          <Service name="API" description={`QA Test Orchestrator · v${query.data.version}`} available={query.data.api === 'available'} />
          <Service name="PostgreSQL" description={query.data.database === 'available' ? 'Conexão com o banco confirmada' : 'Verifique o serviço e a configuração do banco'} available={query.data.database === 'available'} />
        </div>
        <p className="last-checked">Última verificação: {new Date(query.dataUpdatedAt).toLocaleTimeString('pt-BR')}</p>
      </>}
    </div>
  </section>;
}

function Service({ name, description, available }: { name: string; description: string; available: boolean }) {
  return <article className="service"><div className="service-heading"><h3>{name}</h3><span className={available ? 'availability' : 'availability offline'}><span aria-hidden="true" />{available ? 'Disponível' : 'Indisponível'}</span></div><p>{description}</p></article>;
}
