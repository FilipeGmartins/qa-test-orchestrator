# Changelog

## 2026-09-21 — 0.11.0 — Testes de frontend por URL
- Validação: 89 backend, 51 frontend, 13 E2E e 2 testes Node aprovados, builds e migrações alinhados; PostgreSQL continua para #10.
- Tela com projeto/ambiente/URL, seleção de verificações e tamanhos de tela, revisão e criação Pending sem execução automática.
- Nove casos confiáveis para carregamento/console/rolagem horizontal em desktop/tablet/celular; screenshots e trace em falha no histórico existente.
- Suíte por projeto criada atomicamente e reutilizada, URL específica no snapshot sem alterar ambiente, autoria e permissões preservadas.
- Bloqueio de redirects HTTP no runner após teste real detectar escape da rota; casos frontend também bloqueiam requisições de escrita. Origem deve estar aprovada pelo mantenedor.
- Migração AddPageAudits e documentação operacional; PostgreSQL/Docker segue para a etapa final.


## 2026-09-21 — 0.10.0 — Autenticação, autoria e permissões
- Validação: 85 backend, 48 frontend e 12 E2E aprovados; builds e alinhamento de migração aprovados. PostgreSQL permanece para #10.
- Login obrigatório, cookie HttpOnly/SameSite, antiforgery em escritas, sessões de 8 horas e revogação no servidor.
- Administração de usuários e perfis globais Admin/Operator/Reader; troca de senha, bloqueio por tentativas, limitação de requisições e proteção do último administrador.
- Autoria de criação/enqueue/cancelamento determinada pela sessão; evidências e dados protegidos pela API.
- Interface de login, Minha conta e Usuários; consultas mantidas para Leitor, ações de escrita restritas.
- Migração AddAuthentication, SQL e script interativo de bootstrap sem senha padrão. Integração PostgreSQL/infraestrutura continua para #10.


## 2026-09-16 — 0.9.0 — Presets reutilizáveis
- Validação: 76 testes backend, 45 frontend e 10 E2E aprovados; repetições por timeout local detalhadas em TESTING.md. Modelo alinhado à migração.
- Configurações por projeto com criação/edição no assistente, pesquisa, paginação, arquivamento lógico e revisões preservadas.
- Revisão revalidada antes de reutilizar; comparação de versão/fingerprint e transação com tokens de projeto/preset protegem alterações concorrentes.
- Reutilização cria Pending sem enfileirar testes, preservando presetId/presetRevision e snapshot atualizado; Production bloqueado.
- Migração AddRunPresets, SQL completo e teste PostgreSQL ampliado para a etapa final. Dashboard #6 atualizado no roadmap após o bloqueio de uso anterior.

## 2026-09-16 — 0.8.0 — Dashboard e métricas reais
- Validação: 72 testes backend, 42 frontend e 9 E2E aprovados (123 nesta etapa), builds e alinhamento de modelo aprovados.
- Endpoint de métricas com filtros por projeto/período UTC (até 90 dias); agregação no banco sobre resultado final por caso/navegador.
- Totais, taxa de sucesso sem retries duplicados, duração média da última tentativa avaliada, série diária, execuções/falhas recentes e recuperação em retry.
- Histórico sem detalhes explicitamente fora dos indicadores de testes; Error/Cancelled/Running separados. Sem dados fictícios quando o banco falha.
- Dashboard com Recharts carregado sob demanda, alternativa textual, layout móvel, busca/paginação de projetos e navegação para investigação.
- Sem migração nova; PostgreSQL/Compose continua para #10. Próxima implementação: Presets (#7).

## 2026-09-16 — 0.7.0 — Resultados e Evidências
- Tentativas normalizadas e idempotentes por execução/caso/navegador/retry; duração, erro, stack e logs sanitizados e limitados.
- Screenshot, vídeo e trace registrados com UUID; download vinculado à execução, sem caminhos públicos, com bloqueio de expiração e links de filesystem.
- Interface de resultados com filtros, detalhes de falha, downloads e histórico por caso acessível também pelo catálogo.
- Retenção configurável (14 dias por padrão), limpeza de diretórios de execuções terminais, preservando metadados e histórico.
- Migração AddResultsAndArtifacts e SQL completo. Registros anteriores mantêm o resumo; não inventar detalhes ausentes.
- 66 testes backend, 38 frontend, 8 E2E e 1 suíte Node aprovados (113 locais). Capturas reais baixadas pela API na integração Chromium/SQLite. PostgreSQL/Compose permanece para #10; autorização por usuário para #8.

## 2026-09-16 — 0.6.0 — Runner Playwright real
- Worker separado e fila persistente com claim otimista, lease/heartbeat, recuperação sem replay automático e cancelamento de árvore de processos.
- Catálogo fixo page-title/http-ok, origens aprovadas e revalidação antes de enqueue; runner desabilitado por padrão, Production bloqueado.
- Adapter .NET → JSON stdin → Node → Playwright CLI real, workers/retries/browsers/timeout global e políticas de captura.
- Progresso real, tentativas, resumo final e confirmação de execução na interface; detalhes/downloads de evidências ainda pendentes.
- Migração AddRunnerLeases e SQL completo; backfill ProgressJson=[] preserva leitura de execuções anteriores.
- 60 testes backend (incluindo integração real), 36 frontend, 7 E2E da interface e 1 suíte Node com cinco cenários reais/capturas: 104 testes locais aprovados. PostgreSQL real permanece pendente para etapa final.

## 2026-09-16 — 0.5.0 — Configuração e Histórico de Execuções
- Assistente de cinco etapas: suíte, casos/tags, ambiente, opções e revisão.
- Configuração salva como Pending, com snapshot imutável do catálogo e parâmetros validados.
- Histórico geral/por projeto, paginação, filtro por estado, detalhes e cancelamento idempotente com versão.
- Máquina de estados no domínio; concorrência protege criação versus alteração do catálogo e cancelamento versus conclusão.
- Migração AddTestRuns, SQL completo e teste PostgreSQL ampliado; integração real adiada para a última etapa.
- 54 testes backend, 35 frontend e 7 de navegador aprovados (96). Runner ainda não integrado; nenhuma execução real ou resultado simulado.

## 2026-09-15 — 0.4.0 — Casos, Ambientes e Roadmap
- Repositório privado criado no GitHub, código versionado e dez issues organizadas em quadro Kanban.
- Cadastro/edição de casos por suíte, chave permanente única, revisão, tags, status, busca e paginação.
- Ambientes por projeto com URL HTTP(S) validada, unicidade por tipo e Production desabilitado.
- Concorrência e bloqueio de escrita em projetos arquivados; migração AddCasesAndEnvironments e SQL completo.
- 42 testes backend, 25 frontend e 6 de navegador aprovados (73); PostgreSQL real permanece pendente.
- Docker/PostgreSQL mantido como última etapa por decisão do usuário. Próxima entrega: configuração, estados e histórico de Execuções.

## 2026-09-14 — 0.3.0 — Suítes de Teste
- Retomado desenvolvimento do produto; configuração do Windows deixada em pausa após habilitação dos pré-requisitos.
- Cadastro, listagem por projeto, busca/paginação, consulta, edição e ativação/inativação de suítes. Tags validadas, normalizadas e deduplicadas; status enum; nomes/descrições limitados.
- FK de suíte para projeto com Restrict, tokens de concorrência e alteração atômica da versão do projeto para impedir gravação concorrente ao arquivamento.
- Nova migração AddTestSuites, snapshot atualizado e SQL idempotente completo. Readiness verifica ambas as tabelas.
- Interface responsiva com seleção de projeto, lista e formulário; projeto arquivado em modo consulta; HTTP compartilhado entre features.
- 32 testes backend, 19 frontend e 5 testes de navegador aprovados. Um teste PostgreSQL pendente. Build frontend/backend aprovado; modelo e migração sem divergências.
- Casos de Teste e Ambientes permanecem como próxima parte do catálogo; integração com runner não antecipada.

## 2026-09-14 — Preparação do Docker
- Docker Desktop encontrado na instalação por usuário; CLI e Compose disponíveis. Configuração Compose validada com `docker compose config --quiet`.
- Engine não iniciou porque VirtualMachinePlatform estava desabilitado. Firmware já reporta virtualização habilitada.
- Plataforma de Máquina Virtual e WSL habilitados com sucesso, sem reinício automático. Windows informou necessidade de reinicialização.
- Containers, migração no PostgreSQL real e suíte de integração permanecem pendentes até reiniciar o Windows e iniciar o engine Docker.

## 2026-09-14 — 0.2.0 — Projetos
- Implementados entidade Project, regras de nome/descrição, datas UTC, versão e arquivamento lógico sem exclusão física.
- ProjectService e IProjectStore separam casos de uso e persistência; endpoints de criar/listar/consultar/editar/arquivar com OpenAPI, logging e erros 400/404/409/503 consistentes.
- Busca, filtros de status e paginação; nomes duplicados permitidos. Controle de concorrência no serviço e no EF impede sobrescrita por versão antiga.
- Migração InitialProjects, snapshot e SQL idempotente gerados; Compose passa a executar job explícito de migração antes do backend. Readiness valida schema além da conexão.
- Interface de Projetos com formulário validado, detalhe, edição, filtros, paginação e confirmação de arquivamento; preenchimento preservado em falhas.
- Documentação de produto, arquitetura, dados, API, instalação e testes atualizada. Próxima feature: Suítes.

### Verificações
- 23 testes backend, 14 frontend e 4 navegador aprovados (41 no total); builds .NET e TypeScript/Vite aprovados.
- Uma suíte de migração/CRUD PostgreSQL criada, mas ignorada sem QA_TEST_DATABASE. SQLite nos testes não comprova PostgreSQL real.
- API local atualizada responde 503 DATABASE_UNAVAILABLE para projetos sem banco, com mensagem consistente sem detalhes internos; OpenAPI contém os novos endpoints.
- Formulário conferido visualmente em desktop e celular, sem erros JavaScript ou rolagem horizontal; mensagem de indisponibilidade verificada contra a API real.
- Migração e SQL gerados com sucesso; Compose/PostgreSQL real continuam pendentes enquanto Docker é instalado.

## 2026-09-14 — 0.1.0 — Planejamento e fundação
- Requisitos analisados, MVP delimitado, backlog priorizado e modelo de dados inicial documentado.
- Arquitetura modular escolhida; runner e entidades de negócio serão introduzidos incrementalmente.
- Implementada base React/TypeScript/Vite/Tailwind com Router, TanStack Query, navegação responsiva e diagnóstico de serviços com polling, carregamento, erro e nova tentativa.
- Implementada API .NET 10 em quatro camadas com EF Core/Npgsql, OpenAPI em Development, liveness, readiness e middleware central de exceções. Logs estruturados em JSON.
- Dockerfiles, proxy Nginx, Compose local com três serviços, volume PostgreSQL e instruções de inicialização.
- Lockfiles npm e NuGet gerados; SDK e navegadores de teste instalados em diretórios locais ignorados pelo Git.
- Vulnerabilidade transitiva detectada na primeira restauração OpenAPI corrigida atualizando pacotes; nova restauração passou. npm reportou zero vulnerabilidades na instalação.

### Validação executada
- Backend: build e 7 testes xUnit aprovados, sem warnings; banco substituído por probe nos testes HTTP.
- Frontend: 6 testes Vitest aprovados; TypeScript e build Vite de produção aprovados.
- Navegador: 2 testes Playwright/Chromium aprovados (navegação e layout móvel), com API controlada nos testes.
- Integração local real UI → proxy Vite → API: sem erros JavaScript; verificação visual desktop 1440px e móvel 390px; sem overflow horizontal.
- API real sem PostgreSQL: `/api/system` reporta `database: unavailable`, liveness 200 e readiness 503.

### Limitações e próximo passo
- Docker não está instalado: Compose, imagens Linux e conectividade real com PostgreSQL ainda não foram executados. O aceite da infraestrutura permanece pendente dessa verificação.
- Não há tabelas, CRUD, execuções orquestradas nem autenticação nesta entrega. A página informa explicitamente o estágio do produto.
- Próxima entrega: CRUD de Projetos, primeira migração EF Core, testes de persistência PostgreSQL e interface de cadastro/edição/arquivamento.
