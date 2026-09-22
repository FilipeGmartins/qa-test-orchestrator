# Testes e validação

## CI — 2026-09-22

Workflow `.github/workflows/ci.yml` implementado sem Docker. Comandos validados localmente: restore NuGet com `--locked-mode`, `dotnet tool restore`, build/test/publish Release, verificação de modelo EF e SQL idempotente. Backend: 89 aprovados e 1 PostgreSQL ignorado. Frontend: `npm ci`, 51 Vitest, build e 13 Playwright com `CI=true`, `--workers=1 --forbid-only --reporter=line,html`. Node: 2 integrações reais Chromium aprovadas. Workflow validado com actionlint. Total: 155 testes aprovados. Essa validação Windows não substitui a primeira execução Ubuntu no GitHub; consulte a aba Actions e [CI-CD.md](CI-CD.md).

## Frontend por URL — 0.11.0
89 testes backend, 51 frontend, 13 E2E da interface e 2 testes Node aprovados (155 verificações distintas). Backend inclui integração real API → fila SQLite → Chromium para pageUrl diferente da base do ambiente, três resultados persistidos e capturas baixadas com dimensões PNG verificadas. PostgreSQL continua ignorado para a etapa #10.

Node comprova nove combinações de verificações/telas e dimensões 1440×900, 768×1024, 390×844; páginas com recurso 404, console.error/erro JS e overflow reprovam; redirect para servidor não aprovado e POST são bloqueados sem atingir os destinos. O teste encontrou escape de redirects na abordagem anterior; a correção bloqueia respostas 3xx com Location antes de devolvê-las ao browser. Os casos antigos do runner também passaram na regressão.

API cobre Pending sem execução automática, reutilização de uma suíte/nove casos, preservação do ambiente, autoria, URL inválida/origem negada, escolha inválida, ambiente de outro projeto, suíte inativa, ambiente desabilitado, projeto arquivado, Reader e revalidação da política no runner. Vitest verifica revisão obrigatória, mesma origem e preservação em erro. E2E atravessa seleção → revisão → Pending → confirmação → resultado, e verifica layout móvel; respostas de interface são controladas. Capturas desktop/móvel revisadas. Build e alinhamento de migração aprovados; SQL SchemaWithPageAudits.sql gerado.

## Autenticação — 0.10.0
85 testes backend, 48 frontend e 12 E2E aprovados (145 verificações distintas). Builds aprovados e modelo sem migração pendente. PostgreSQL continua ignorado sem QA_TEST_DATABASE; aceite de infraestrutura permanece para #10.

HTTP/SQLite usa cookie real e antiforgery: acesso anônimo, papéis Reader/Operator/Admin, escrita bloqueada, autoria derivada da sessão, logout e replay de cookie, token de outro navegador, senha atual obrigatória, revogação de todas as sessões, expiração, desativação, versões antigas, último administrador, concorrência do registro administrativo, bloqueio após falhas, limite 429 e bootstrap único. Runner Chromium real passa autenticado e verifica autoria de enqueue. Hosts de teste usam proteção de dados efêmera; produção conserva o provedor normal do ASP.NET Core. Teste PostgreSQL foi adaptado para bootstrap/login após migração, mas não foi executado nesta etapa.

Vitest: três testes novos protegem páginas, limpeza do cache na entrada/revogação e erro de conexão. Os 45 existentes passaram na regressão; um mock de sessão dos novos testes foi corrigido e os três foram repetidos com sucesso. Navegador: os dez fluxos existentes passaram com sessão controlada explícita; dois novos fluxos de login/leitor/logout e administração de usuário passaram após corrigir a interceptação de rotas do teste. E2E de interface usa API controlada; não confundir com integração PostgreSQL real.

Comandos: `npm test -- --pool=threads --maxWorkers=1 --testTimeout=30000`; `npm run test:e2e -- --workers=1`; backend com QA_RUNNER_INTEGRATION=1. Bootstrap interativo somente após migração e com banco configurado, conforme AUTHENTICATION.md.

## Presets — 0.9.0
76 testes backend, 45 frontend e 10 E2E aprovados (131 verificações distintas). Backend inclui runner real Chromium com PNG/WebM/ZIP; PostgreSQL permanece ignorado sem QA_TEST_DATABASE, até a etapa #10. Cobertura nova: revisões preservadas, origem da execução, arquivamento, concorrência, revalidação de ambiente/casos, isolamento por projeto, configuração inválida e repetição com versão antiga sem duplicar execuções.

Interface e navegador cobrem revisão explícita, Pending sem executar, edição, remoção de casos selecionados e histórico após arquivamento. Os testes da interface usam API controlada. Migração AddRunPresets e SQL gerados; nenhuma divergência entre modelo e migrações.

Nesta máquina, executar sequencialmente evita pressão de recursos: `npm test -- --pool=threads --maxWorkers=1` e `npm run test:e2e -- --workers=1`. A regressão final teve timeout no teste de Projetos (5 s), aprovado ao repetir o arquivo com `--testTimeout=30000`; os outros 37 testes frontend passaram na execução completa. O E2E do dashboard foi aprovado ao repetir com espera inicial de 15 s para o módulo lazy compilado pelo Vite; os outros nove E2E passaram na regressão completa. O teste de runner real recebeu 60 s para inicialização do Chromium e geração de evidências, sem alterar os limites de produção.

## Dashboard — 0.8.0
72 testes backend, 42 frontend e 9 E2E de navegador aprovados nesta etapa (123). Backend inclui integração real do runner Chromium; PostgreSQL continua ignorado até #10. Novos testes verificam última tentativa, separação de browsers, timeout, exclusão de Running/Cancelled, histórico antigo sem detalhes, projeto, limite inclusivo de data UTC, dados vazios e períodos inválidos. Teste PostgreSQL foi ampliado para consultar o dashboard quando essa integração for habilitada.

Interface: filtros aplicados somente ao confirmar, falha/recarregamento, cobertura, links de investigação e ausência de duração sem avaliações. E2E valida gráfico Recharts, projeto/datas, série textual e layout móvel sem overflow; screenshots desktop/móvel revisadas. Builds aprovados e nenhuma mudança de modelo pendente. A suíte Node existente não foi alterada nesta etapa.

## Resultados — 0.7.0
66 testes backend, 38 frontend, 8 E2E da interface e 1 suíte Node aprovados: 113 locais. Cobertura nova: registro idempotente, vínculo ao caso, filtros, paginação, texto sanitizado, download por execução, expiração, caminho adulterado e limpeza preservando histórico/execuções ativas. Integração .NET real executa Chromium, registra PNG/WebM/ZIP e baixa os três via API comparando tamanhos. SQLite é exclusivo dos testes.

E2E percorre criar configuração → confirmar execução → acompanhar → falha → download → histórico do caso → filtro, com API controlada. Node comprova capturas reais, associação dos arquivos e falhas/retries. PostgreSQL continua ignorado até #10; autenticação por usuário entra em #8.

## Suítes — 0.3.0
32 testes backend, 19 frontend e 5 testes de navegador aprovados (56 no total). A suíte PostgreSQL permanece ignorada sem QA_TEST_DATABASE. Adicionados testes de tags, normalização, limites, status inválido/numérico, ciclo criar/editar/inativar/reativar, paginação, projeto inexistente, isolamento entre projetos, projeto arquivado e rollback quando arquivamento concorrente vence criação de suíte.

Vitest cobre formulário, validação de tags, preenchimento preservado em conflito, leitura em projeto arquivado e erro/recarga. Playwright cobre criação e inativação da suíte com consulta pelo filtro. As respostas do navegador são controladas; a suíte opcional PostgreSQL agora também verifica criação e persistência das tags e status de Suítes.

Migração AddTestSuites gerada; `migrations has-pending-model-changes` não detectou divergência. Script completo: `docs/migrations/SchemaWithTestSuites.sql`. A validação real de containers aguarda o engine Docker após reinício do Windows. Não apresentar os testes com SQLite como validação PostgreSQL.

## Projetos — 0.2.0
Resultado local: 23 testes backend aprovados (domínio, HTTP/SQLite e diagnóstico), 14 Vitest aprovados e 4 Playwright aprovados. Builds .NET/TypeScript/Vite aprovados. Uma suíte PostgreSQL foi ignorada porque QA_TEST_DATABASE não foi definido.

Cobertura adicionada: trim e limites, criação/consulta/edição/arquivamento, preservação de histórico, arquivamento repetido, proibição de editar arquivados, busca/paginação/filtros, JSON e query inválidos, 404, versão ausente/antiga e conflito entre dois contextos que carregaram o mesmo registro. UI: obrigatório, envio, preservação do formulário em erro, filtros, edição concorrente e confirmação. Navegador: fluxo completo de Projetos e formulário móvel com erro de banco.

### Validar com PostgreSQL assim que Docker estiver pronto
Na raiz, PowerShell:

Encerre antes a API local que ocupa 5080, se estiver aberta, para liberar a porta do Compose.

```powershell
docker compose up --build -d
# Readiness deve responder 200 após o job migrate terminar.
Invoke-RestMethod http://localhost:5080/api/health/ready
$env:QA_TEST_DATABASE = 'Host=localhost;Port=5432;Database=qa_orchestrator;Username=qa;Password=qa_local_only;Timeout=5'
dotnet test backend/QaTestOrchestrator.slnx --filter FullyQualifiedName~PostgresProjectTests
```

Se alterou a senha em `.env`, ajuste QA_TEST_DATABASE. Use a instalação local do SDK conforme README se necessário. A suíte exige permissão para criar schemas e cria um schema `qa_test_<UUID>` exclusivo; aplica e reaplica migração, verifica schema pendente/readiness, CRUD/filtros e conflito. Ao terminar remove somente esse schema. Não use um banco de produção para testes.

Abra `/projects` e crie, edite e arquive um projeto. Recarregue a página, filtre Arquivados e confirme a persistência. Pare/reinicie os containers **sem `-v`** e confirme que o registro permanece. Essa verificação não foi executada nesta entrega por ausência de Docker.

Para revisar modelo/migração sem conectar: `dotnet ef migrations has-pending-model-changes --project backend/src/Infrastructure --startup-project backend/src/Api`. A ferramenta dotnet-ef 10.0.12 foi instalada localmente em `.tools/ef`; ao usá-la nesta máquina, defina DOTNET_ROOT/PATH para `.tools/dotnet` e NUGET_PACKAGES para `.cache/nuget`.

## Fundação
- `cd frontend` → `npm ci` → `npm test` → `npm run build`.
- `dotnet test backend/QaTestOrchestrator.slnx`: testes em host de teste, probe substituído no diagnóstico e SQLite nas operações de Projetos. A suíte PostgreSQL só executa quando QA_TEST_DATABASE está configurado.
- `cd frontend` → `npx playwright install chromium` → `npm run test:e2e`: smoke da interface com resposta HTTP controlada. Integração real completa entra com runner.
- `docker compose up --build`: validar página em http://localhost:8080, readiness e OpenAPI. Parar PostgreSQL deve mudar readiness para 503 e a página para banco indisponível; restaurar banco deve recuperar no próximo polling. `docker compose down` preserva volume; não usar `-v` se quiser preservar dados.

## Próximas features
Projetos/suítes: testes de validação e persistência real PostgreSQL. Execuções: criação, limites, integridade de projeto/suíte/ambiente/casos, transições, concorrência, SuccessRate (incluindo zero e ignorados). Runner: whitelist, timeout, cancelamento, crash, retries, parsing, idempotência e nenhum shell arbitrário. UI: wizard, erros por campo, revisão e polling. E2E obrigatório do MVP: criar execução real, acompanhar, consultar resultado e filtrar histórico.

## Registro desta entrega
7 testes xUnit, 6 testes Vitest e 2 testes Playwright aprovados. Build frontend e backend aprovados. UI verificada visualmente em desktop/móvel contra a API real sem banco. Compose e PostgreSQL real não executados por ausência de Docker. Consulte os detalhes no CHANGELOG. Não considerar Compose validado apenas por inspeção dos arquivos.

Nesta máquina os navegadores foram instalados em `.cache/ms-playwright`. Antes de rodar os testes a partir de `frontend` no PowerShell, use `$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path (Split-Path $PWD) '.cache\ms-playwright'`; em outra máquina, `npx playwright install chromium` instala no cache padrão. SDK e pacotes locais do backend: ver instruções no README.


## Catálogo — 0.4.0
42 testes backend aprovados, 25 frontend e 6 Playwright (73 locais). Cobertura: URLs inválidas e credenciais, bloqueio Production, chave estável/normalização/duplicidade, revisão, filtros/paginação, isolamento por projeto/suíte, versão antiga, arquivamento e rollback concorrente. Interface: validação antes de enviar, preservação em erro, formulário Production bloqueado; navegador percorre criar/editar/inativar caso e configurar Staging. Testes de interface usam API controlada; não comprovam integração de banco. Teste PostgreSQL ampliado para casos/ambientes e todas as migrações, ignorado enquanto QA_TEST_DATABASE não for informado. Validação real ficará na etapa final #10.


## Execuções — 0.5.0
54 testes backend, 35 frontend e 7 testes Playwright aprovados (96 locais). Novos cenários: limites, vínculos entre projetos, suíte/ambiente ativos, seleção de casos, tags, snapshot preservado após editar catálogo, estados terminais, cancelamento idempotente, concorrência na criação e cancelamento/conclusão. Navegador: assistente de cinco etapas, revisão, Pending, cancelamento e filtro do histórico. Teste PostgreSQL ampliado para criação/consulta/cancelamento, ainda ignorado sem QA_TEST_DATABASE. Testes de frontend usam API controlada, sem comprovar execução Playwright real.


## Runner — 0.6.0
60 testes backend aprovados com QA_RUNNER_INTEGRATION=1 (um teste PostgreSQL continua ignorado), 36 frontend, 7 E2E e 1 suíte Node: 104 verificações automatizadas locais. A suíte Node realiza sucesso real com Chromium, falha/retry, timeout, chave rejeitada, origem negada e confirma arquivos PNG/WebM/ZIP. Integração .NET cobre API, enqueue, fila SQLite, processo Node/browser, progresso/resultados persistidos e interrupção do adapter. Testes adicionais verificam reivindicação concorrente, cancelamento em execução, idempotência e recuperação de lease expirado sem replay.

Comando a partir da raiz: `node --test automation/runner.test.mjs`. Para teste .NET real, configure QA_RUNNER_INTEGRATION=1 e PLAYWRIGHT_BROWSERS_PATH antes de `dotnet test backend/QaTestOrchestrator.slnx`. Sem a variável, teste real é ignorado (requer Node/Chromium instalados). Testes padrão não iniciam worker de produção. E2E da interface usa API controlada; integração real usa Chromium e SQLite isolado, não valida PostgreSQL/Compose. Firefox/WebKit/Headed dependem dos browsers/display do host e não foram executados nesta validação. Documentação de operação em automation/README.md.
