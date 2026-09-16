# Testes e validação

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
