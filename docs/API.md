# API

## Dashboard — v0.8.0
`GET /api/dashboard?projectId={uuid}&from=YYYY-MM-DD&to=YYYY-MM-DD`. ProjectId opcional (inclui arquivados); datas UTC inclusivas, 1–90 dias, sem futuro. Padrão: últimos 30 dias incluindo hoje; só to informado: 30 dias terminando nessa data; só from: até hoje. Filtro usa CreatedAt da execução. 400 para período inválido; 404 para projeto inexistente.

Resposta: from/toExclusive/generatedAt, totalRuns/runCounts por estado, completedRunsWithoutDetails, passed/failed/skipped/interrupted, successRate, averageTestDurationMs, flakyTests, daily, recentRuns, recentFailures, flakyCases. Listas recentes/instabilidade limitadas a 8 e dias sem registros preenchidos com zero. Nunca retorna zeros como substituto de banco indisponível.

Métricas de testes usam a última tentativa por RunId/CaseId/Browser apenas em execuções Passed/Failed; timeout conta como falha. Taxa: passed/(passed+failed), zero sem avaliados. Média em ms das últimas tentativas avaliadas, null sem avaliações. Flaky: resultado final aprovado com tentativa anterior failed/timedOut na mesma execução. Execuções antigas sem TestAttempts entram no total de execuções, mas são explicitamente excluídas dos indicadores de testes. Contagens refletem consultas ao banco; durante novas conclusões concorrentes, seções podem atualizar entre consultas. Polling a cada 30 segundos.

## Resultados — v0.7.0
- `GET /api/test-runs/{id}/results` e `GET /api/test-cases/{id}/results`: `{items,total,page,pageSize}`. Query: status=all/passed/failed/timedOut/skipped/interrupted; browser=all/Chromium/Firefox/WebKit; page=1–100000; pageSize=1–100 (padrão 12).
- Cada item: id, runId, caseId, caseName, stableKey, browser, attempt (zero-based), status, durationMs, error, stack, logs, recordedAt e artifacts. Ordem recordedAt/id decrescente. Total conta tentativas, incluindo retries.
- Artefato público: id, kind (screenshot/video/trace), size em bytes, expiresAt UTC, available. Nenhum path retornado.
- `GET /api/test-runs/{id}/artifacts/{artifactId}`: attachment binário com nome gerado, no-store e nosniff. 404 se inexistente, de outra execução, expirado, removido ou com caminho inválido; suporta Range. Apenas GET, sem upload nem paths fornecidos pelo cliente.
- Recursos de consulta inexistentes retornam 404; filtros/paginação inválidos retornam 400. Autorizações por usuário ainda não implementadas; uso local até #8.

Resultados anteriores à versão 0.7 podem retornar lista vazia, mantendo o resumo antigo. Progress continua begin/attempt/end; detalhes de duração, falha e arquivos agora estão na consulta de resultados.

## Disponível na fundação
- GET `/api/system`: 200, `{ application, version, api, database }`; database é `available` ou `unavailable`. Diagnóstico não expõe connection string nem detalhes de erro.
- GET `/api/health/live`: 200, `{ status: "healthy" }`; indica apenas processo vivo.
- GET `/api/health/ready`: 200 ou 503, `{ status: "ready" | "not_ready" }`; verifica banco.
- GET `/openapi/v1.json`: documento OpenAPI gerado. Disponível no ambiente Development; Compose é ambiente local Development.

Falhas inesperadas: HTTP 500, `{ error: "INTERNAL_ERROR", message: "Não foi possível concluir a solicitação.", traceId }`. Stack trace só no log do servidor. Cancelamento pelo cliente não é transformado em erro interno.

## Contratos planejados, ainda não implementados
Execuções: GET/POST `/api/test-runs`; GET `/api/test-runs/{id}`; POST `/api/test-runs/{id}/cancel`; GET `/api/test-runs/{id}/executions`. Dashboard: GET `/api/dashboard`. Presets: GET/POST `/api/presets`. Detalhe de execução e download de artefatos serão especificados na respectiva entrega.

POST de execução aceitará somente configuração estruturada; 202 + Location após persistência. Valores fora de whitelist: 400 INVALID_CONFIGURATION; entidade ausente: 404; transição inválida: 409. Listagens usarão page/pageSize limitados e filtros de projeto/status/data. Autenticação será obrigatória antes de liberar execução compartilhada.

## Projetos — disponível na versão 0.2.0
- GET `/api/projects?search=portal&status=active&page=1&pageSize=20`: `{ items: ProjectDto[], total, page, pageSize }`. Defaults: busca vazia, active, 1 e 20. page entre 1 e 100000; pageSize entre 1 e 100; status aceita exatamente active/archived/all. Busca por trecho literal de nome, sem distinção de maiúsculas/minúsculas; até 120 caracteres. Ordenação CreatedAt DESC, Id ASC.
- POST `/api/projects`: `{ "name": "Portal cliente", "description": "Jornada de compras" }` → 201 + Location + ProjectDto.
- GET `/api/projects/{uuid}` → 200 + ProjectDto ou 404, inclusive consulta a arquivados.
- PUT `/api/projects/{uuid}`: `{ "name": "Portal atualizado", "description": "Novo escopo", "version": "UUID recebido no GET" }` → 200 + ProjectDto com nova versão. Campos de nome e descrição substituem os anteriores; descrição omitida vira string vazia.
- POST `/api/projects/{uuid}/archive`: `{ "version": "UUID recebido no GET" }` → 200 + ProjectDto arquivado. Repetição após já arquivado retorna o mesmo registro; não há exclusão física nem reativação.

ProjectDto: id, name, description, createdAt, updatedAt, archivedAt (null quando ativo), version. Datas em UTC. Nome trim 1–120; descrição trim 0–2000; nomes duplicados permitidos.

Erros seguem `{ error, message, traceId, field? }`: 400 INVALID_CONFIGURATION (com field), 400 INVALID_REQUEST (binding/JSON inválido), 404 PROJECT_NOT_FOUND, 409 CONCURRENT_UPDATE ou PROJECT_ARCHIVED, 503 DATABASE_UNAVAILABLE. Campos/version são validados no servidor. Cliente deve recarregar antes de reenviar após conflito. Readiness agora também verifica migrações pendentes e acesso à tabela Projects.

## Suítes — disponível na versão 0.3.0
- GET `/api/projects/{projectId}/test-suites?search=smoke&status=all&page=1&pageSize=20`: `{ items, total, page, pageSize }`. Status de filtro all/active/inactive, default all; mesmos limites de paginação de Projetos. Lista também em projetos arquivados.
- POST `/api/projects/{projectId}/test-suites`: `{ "name": "Smoke", "description": "Jornada crítica", "tags": ["@smoke"], "status": "Active" }` → 201 + Location + SuiteDto. Status omitido na criação assume Active.
- GET `/api/test-suites/{id}` → SuiteDto; 404 TEST_SUITE_NOT_FOUND se ausente.
- PUT `/api/test-suites/{id}`: name, description, tags, status (obrigatório Active/Inactive), version (UUID atual). Retorna nova versão. Inativar usa o mesmo PUT, preservando dados. ProjectId é imutável.

SuiteDto: id, projectId, name, description, tags, status, createdAt, updatedAt, version. Status JSON numérico é rejeitado. Nome trim 1–120, descrição até 2000, no máximo 20 tags; tags são normalizadas para minúsculas, deduplicadas e devem corresponder a `^@[a-z0-9][a-z0-9_-]{0,39}$`.

Projeto ausente → 404; projeto arquivado → 409 PROJECT_ARCHIVED nas gravações; edição concorrente da suíte/projeto → 409 CONCURRENT_UPDATE. Gravações de suíte também alteram a versão do projeto; refaça GET antes de editar/arquivar o projeto. Não há exclusão física. Casos de teste e ambientes ainda não têm endpoints.


## Casos e Ambientes — v0.4.0
- `GET /api/test-suites/{suiteId}/test-cases?search=&status=all&page=1&pageSize=20`: `{items,total,page,pageSize}`; filtros all/active/inactive, página 1–100000, tamanho 1–100.
- `POST /api/test-suites/{suiteId}/test-cases`: `{stableKey,name,description,tags,status}`; 201 com Location do caso. Status padrão Active.
- `GET /api/test-cases/{id}`: consulta, incluindo chave/revisão e versão.
- `PUT /api/test-cases/{id}`: `{name,description,tags,status,version}`; chave e suíte não são editáveis. Status e versão obrigatórios.
- `GET /api/projects/{projectId}/environments`: array de ambientes (até três).
- `POST /api/projects/{projectId}/environments`: `{name,baseUrl,enabled}`; tipo obrigatório Development/Staging/Production, enabled padrão false. 201 com Location da coleção.
- `PUT /api/environments/{id}`: `{baseUrl,enabled,version}`; tipo e projeto imutáveis.

URLs HTTP(S) sem credenciais, parâmetros ou fragmentos. Production habilitado retorna 400. Catálogo não executa chamadas para URLs. Casos retornam `catalogVersion` incremental e `version` UUID para concorrência. Ambientes retornam `version` UUID. Projeto arquivado retorna 409. Chave/tipo repetido ou versão antiga retorna 409 CONCURRENT_UPDATE. Item inexistente retorna 404 CATALOG_NOT_FOUND; projeto/suíte inexistente mantém os códigos específicos. Status/tipos numéricos rejeitados.


## Execuções — v0.5.0
- `POST /api/projects/{projectId}/test-runs`: cria configuração Pending, HTTP 201 com Location `/api/test-runs/{id}`.
- `GET /api/test-runs?projectId=&status=Pending&page=1&pageSize=20`: histórico geral ou por projeto. Omitir projectId para todos. Status all/Pending/Queued/Running/Passed/Failed/Error/Cancelled (nomes exatos). Página 1–100000; tamanho 1–100. Retorno `{items,total,page,pageSize}`.
- `GET /api/test-runs/{id}`: detalhe com configuration (snapshot), status, version, createdAt/startedAt/finishedAt, runnerAvailable=false.
- `POST /api/test-runs/{id}/cancel`: `{version}` obrigatório; retorna estado atualizado. Cancelamento repetido de Cancelled é idempotente. Não exige projeto ativo.

Exemplo do corpo de criação:
```json
{
  "testSuiteId": "UUID", "environmentId": "UUID", "caseIds": ["UUID"], "tags": ["@smoke"],
  "options": {
    "testType": "Smoke", "browser": "Chromium", "mode": "Headless",
    "workers": 1, "retries": 0, "timeoutSeconds": 60,
    "screenshot": "OnFailure", "video": "OnFailure", "trace": "OnFailure"
  }
}
```
Tipos Smoke/Regression/EndToEnd/API/Accessibility; navegadores Chromium/Firefox/WebKit/All; modo Headless/Headed; políticas Always/OnFailure/Never. Workers 1–10, retries 0–5, timeout 5–300. Enums numéricos são rejeitados. 1–100 IDs únicos, ativos, da suíte; tags opcionais até 20, OR por caso. Ambiente deve estar habilitado, fora de Production e no mesmo projeto da suíte. Estado inicial sempre definido pelo servidor. URL, nomes e versões do snapshot são resolvidos no banco, não aceitos do cliente.

404 RUN_NOT_FOUND, 409 CONCURRENT_UPDATE/INVALID_RUN_STATE/PROJECT_ARCHIVED, 400 INVALID_CONFIGURATION/INVALID_REQUEST. Nenhum endpoint público para transições de worker. HTTP 201 significa configuração salva, não teste executado. Configuração imutável; uma nova chamada de criação gera outra solicitação.


## Runner — v0.6.0
- `GET /api/runner`: `{enabled,catalog:[{key,name,types,version}]}`. Enabled indica configuração válida/habilitada, não heartbeat do worker.
- `POST /api/test-runs/{id}/enqueue`: `{version}`. Somente Pending; Queued repetido é idempotente. Revalida estado/versões do catálogo, origem aprovada, chaves e tipo implementados. Não modifica o snapshot. 400 configuração/catálogo/origem/runner inválidos, 409 versão/estado/projeto arquivado.
- Cancelar Running grava `cancellationRequested=true` e retorna Running até o worker encerrar. Cancelar Pending/Queued continua imediato.
- RunDto agora inclui `cancellationRequested`, `progress` (eventos begin/attempt/end), `result` (status,total,passed,failed,skipped) e `runnerError` (mensagem sanitizada). `runnerAvailable` reflete a configuração do servidor. Tentativas incluem key/browser/attempt/status/durationMs. Detalhes de resultados e artefatos completos ficam para a próxima entrega.

Não há endpoint para fornecer scripts, caminhos ou arquivos. Nenhuma rota HTTP pode concluir um job arbitrariamente. API em localhost enquanto autenticação permanece pendente. Configurações antigas Pending exigem confirmação e revalidação; nada é enfileirado automaticamente.
