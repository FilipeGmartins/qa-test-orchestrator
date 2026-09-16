# Runner Playwright — v0.6.0

Executa apenas testes implementados no catálogo versionado `catalog.json`; não importa código, comandos ou caminhos fornecidos pela interface. O adapter .NET envia JSON por stdin ao processo Node com ArgumentList e sem shell. `run.mjs` invoca o CLI Playwright instalado em `frontend/node_modules`, com configuração e arquivos de teste fixos. Instale as dependências com `npm ci` em frontend e os navegadores com `npx playwright install chromium firefox webkit` conforme os navegadores usados.

## Catálogo inicial real
- `page-title`: navega à URL base, exige resposta HTTP válida e título não vazio. Tipos Smoke, Regression e EndToEnd.
- `http-ok`: GET da URL base, exige HTTP 2xx e não segue redirects. Tipos Smoke, Regression e API.

Cadastre um caso com a chave correspondente. Outros casos podem existir como metadados, mas não podem ser enfileirados enquanto não houver implementação no catálogo. Accessibility não está implementado; selecionar esse tipo é rejeitado no enqueue. Nomes e tags do catálogo não geram testes automaticamente. Para ampliar, mantenha o JSON, spec e testes em controle de versão; não aceite plugins ou uploads da UI. Versão de implementação inicial: 1.

## Iniciar localmente
Requer PostgreSQL preparado e migrações aplicadas para usar a interface real. A integração Docker/PostgreSQL será validada por último; SQLite existe somente nos testes.

1. Instale dependências do frontend e os browsers necessários.
2. Inicie `node automation/fixture.mjs` para disponibilizar o alvo controlado em `http://127.0.0.1:5180/`.
3. Configure `Runner__Enabled=true` nos processos API e worker. O padrão é false. AllowedOrigins padrão no Development: `http://127.0.0.1:5180`. Outras origens devem ser aprovadas pelo mantenedor via `Runner__AllowedOrigins__0`, etc.
4. Inicie a API: `dotnet run --project backend/src/Api -- --urls http://127.0.0.1:5080`.
5. Em outro terminal, com o mesmo ambiente/connection string: `dotnet run --no-build --project backend/src/Api -- --worker`. O worker não abre servidor HTTP. No Windows, use o SDK `.tools/dotnet` e configurações locais descritos no README principal, se não houver SDK global.
6. Cadastre projeto, suíte, caso `page-title` ou `http-ok` e ambiente habilitado apontando para o alvo aprovado. Salve configuração como Pending. Abra detalhes → Executar agora → Confirmar execução.

`Runner:Root` é o caminho do diretório automation, relativo ao ContentRoot da API ou absoluto; padrão ../../../automation. `Runner:Node` escolhe o executável instalado pelo mantenedor. `Runner:ArtifactsRoot` padrão ../../../.cache/run-artifacts. API e worker precisam compartilhar configuração, banco e catálogo. Enabled indica configuração habilitada, não comprova que o worker está online. Sem worker, itens permanecem Queued.

## Execução e recuperação
Fila persistida em TestRuns. Um worker processa uma solicitação por vez; Workers controla paralelismo dos testes dentro dela. Reivindicação usa token EF otimista e lease UUID, com expiração de 30 segundos e heartbeat a cada segundo. Outra instância não pode salvar a mesma reivindicação. Progresso e tentativas são gravados incrementalmente; retries preservados, resumo final usa resultado final por caso/navegador. All expande Chromium/Firefox/WebKit. Headed exige display no host.

Cancelamento Pending/Queued é imediato. Running persiste intenção; o adapter encerra a árvore de processos antes de finalizar Cancelled. Timeout global inclui testes e retries; o adapter possui prazo adicional de 10 segundos para encerramento do protocolo. Perda de conexão ou lease interrompe o adapter. Lease expirado termina Error (ou Cancelled se havia intenção), sem executar novamente: o usuário deve criar outra solicitação. Em encerramento abrupto do host, o processo órfão pode durar até o timeout global; não há replay automático que duplique ações.

Evidências obedecem Always/OnFailure/Never e são gravadas sob diretório UUID da execução. O resumo e tentativas aparecem no detalhe; navegação/download autenticado de screenshots, vídeos, traces e retenção entram na próxima etapa. Nada é servido estaticamente e stderr bruto não é persistido.

## Restrições de rede e ambiente
Somente origens HTTP(S) exatas aprovadas; credenciais/query/fragmentos proibidos. Requisições do browser verificam origens, redirects são buscados sem seguimento automático, service workers e WebSockets são bloqueados. GET via API não segue redirects. Estes controles não são sandbox de sistema operacional nem proteção geral contra DNS rebinding/WebRTC; execute apenas contra alvos confiáveis e mantenha a aplicação local. Isolamento de processo, egress em rede e limites de recursos do container serão validados com a infraestrutura final. Production continua bloqueado e autenticação é etapa separada.

## Verificação real sem Docker
- `node --test automation/runner.test.mjs`: servidor HTTP efêmero + Chromium, sucesso, falha/retry, origem negada, chave inválida e timeout.
- `QA_RUNNER_INTEGRATION=1` no processo de testes .NET habilita API → fila SQLite isolada → processo Node/Chromium → resultado persistido e interrupção da árvore de processos.
- Nesta máquina, configure `PLAYWRIGHT_BROWSERS_PATH` com o caminho absoluto `.cache/ms-playwright` na raiz.

Referências: [rede e service workers](https://playwright.dev/docs/network), [APIRequestContext e maxRedirects](https://playwright.dev/docs/api/class-apirequestcontext).
