# Changelog

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
