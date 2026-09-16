# Arquitetura

## Resultados — implementação 0.7.0
Reporter do Playwright copia até 10 anexos PNG/WebM/ZIP por tentativa (máximo 200 MiB cada) para nomes UUID em `run/evidence`. Emite metadados e textos limitados; ResultStore valida chave contra snapshot, navegador, retry, tamanho real e caminho gerado. Tentativa, artefatos e progresso são salvos na mesma transação e protegidos pelo lease/token da execução. Índice único impede duplicar a mesma tentativa. O JSON público de progresso permanece compacto, sem paths; detalhes vêm dos endpoints paginados.

ITestResults define consultas/download; ResultStore implementa com EF e filesystem. Resultados mantêm nome/chave do snapshot e FK do caso, permitindo histórico mesmo após editar o catálogo. Filtros e paginação são aplicados no banco; anexos são buscados apenas para a página atual. UI faz polling enquanto Running e nova consulta ao entrar no estado terminal.

Logs, mensagens e stack têm limite de 4000 caracteres cada e redação de URLs, caminhos, Authorization/Cookie e campos comuns de segredo. Isso reduz exposição, mas não garante remoção de todo dado sensível arbitrário. Binários não são sanitizados. Downloads usam attachment/octet-stream/no-store/nosniff; verificam runId + artifactId, expiração e componentes sem reparse points. Armazenamento deve pertencer exclusivamente ao serviço; estes checks não substituem isolamento contra um usuário local que possa alterar arquivos simultaneamente.

Limpeza ocorre a cada cinco minutos no loop do worker, em lotes de até 20 execuções terminadas há mais que Runner:RetentionDays (1–365, padrão 14). Comando --cleanup-artifacts executa um lote sem iniciar worker ou HTTP. Valida fronteira e árvore sem links antes de apagar somente a pasta UUID de execução registrada; remove também cópias originais, input e capturas órfãs dessa execução. Marca ArtifactsPurgedAt/DeletedAt, sem apagar tentativas. Diretórios sem execução cadastrada não são removidos. Falhas de arquivo são tentadas no próximo ciclo; API/worker devem compartilhar configuração e armazenamento. Sem worker, agendar o comando para manter limpeza física. Autorização por usuário fica para #8; PostgreSQL/Docker para #10.

## Decisão inicial
Monólito modular em ASP.NET Core (.NET 10), React/TypeScript/Vite, Tailwind, React Router e TanStack Query. EF Core com PostgreSQL. Automação Playwright/TypeScript será adicionada na fase 5. Recharts será introduzido com métricas reais. Compose é o ambiente local de referência.

Alternativa: microserviços e broker externo isolariam carga, mas exigiriam observabilidade, entrega e consistência distribuídas já na fundação. Escolha: API única e contratos por camada; worker separado apenas quando integrar o runner, porque browser é uma fronteira de recursos e segurança. Não introduzir repository genérico ou CQRS sem necessidade.

## Camadas e dependências
- Domain: entidades e invariantes sem dependências de infraestrutura. Project controla validação, alteração, arquivamento e versão.
- Application → Domain: diagnóstico via IDatabaseProbe e ProjectService com DTOs, limites de consulta, verificação de versão e IProjectStore.
- Infrastructure → Application: DbContext/Npgsql, adapter do diagnóstico, ProjectStore e migrações; futuramente PlaywrightTestRunner.
- Api → Application e Infrastructure: composição de DI, endpoints HTTP, OpenAPI, middleware de exceções e logging estruturado.
- Frontend → API via HTTP de mesma origem. Vite e Nginx encaminham /api e /openapi; não é necessário liberar CORS globalmente.

## Estrutura
```text
frontend/                  React, estilos, testes Vitest e E2E da interface
backend/
  src/Api/                 endpoints, middleware, configuração
  src/Application/         contratos e casos de uso
  src/Domain/              entidade Project e regras de negócio
  src/Infrastructure/      EF Core, PostgreSQL, integrações
  tests/Api.Tests/          domínio, HTTP/SQLite e integração PostgreSQL (xUnit)
automation/                reservado para runner Playwright (fase 5)
docs/                      produto, arquitetura, API, dados, testes, changelog
docker/                    Dockerfiles e Nginx
docker-compose.yml
```

## Fundação: fluxo implementado
React consulta GET /api/system por TanStack Query. Application consulta IDatabaseProbe; Infrastructure verifica conectividade, migrações pendentes e acesso à tabela Projects. /api/health/live independe do banco; /api/health/ready retorna 503 se o banco estiver inacessível ou schema não estiver pronto.

## Projetos: decisões da segunda entrega
ProjectService orquestra operações; Project protege regras e gera nova Version (UUID) em cada mudança. IProjectStore é uma interface pequena e específica que mantém EF fora da Application; não há repositório genérico. ProjectStore usa tracking para escrita e AsNoTracking para listagem, ordenação estável por CreatedAt DESC/Id, paginação limitada e busca parametrizada. Nomes podem se repetir: não há requisito de unicidade.

Version é token de concorrência do EF, independente do provedor. Cliente envia a versão lida; o serviço rejeita versões antigas, e SaveChanges também detecta conflito entre duas requisições que já carregaram a mesma versão. HTTP 409 exige recarregar. Arquivar novamente um projeto já arquivado é idempotente e não altera timestamps. Não há DELETE ou cascata destrutiva.

Migração InitialProjects gerada com EF Core, incluindo snapshot e SQL idempotente revisável em `docs/migrations`. A API só aplica migrações quando invocada explicitamente com `--migrate`, encerrando após a operação; nunca as aplica no startup web normal. Compose possui job `migrate` entre PostgreSQL saudável e backend (`service_completed_successfully`). Isso evita várias instâncias web migrando ao iniciar. Se a migração falhar, o backend não é iniciado pelo Compose.

Testes HTTP usam SQLite relacional isolado; não comprovam semântica PostgreSQL. Teste opcional com QA_TEST_DATABASE cria schema aleatório exclusivo, aplica/reaplica migrações, testa readiness/CRUD/filtros/conflito e remove somente esse schema. Nenhum dado existente é apagado. EnsureCreated é usado apenas no substituto SQLite dos testes.

Frontend mantém páginas de lista, cadastro, detalhe e edição em `features/projects`; invalida listas após gravações e atualiza o cache do detalhe. Erros preservam preenchimento. HTTP 503 informa banco indisponível/schema pendente, sem retornar detalhes internos. Autenticação segue pendente; acesso desta versão continua restrito a ambiente local.

## Suítes: terceira entrega
TestSuite contém enum Active/Inactive, tags normalizadas, timestamps e token de versão. TestSuiteService usa ITestSuiteStore e IProjectStore no mesmo DbContext scoped. Alterar o catálogo também atualiza UpdatedAt/Version do projeto; isso impede gravar uma suíte com um projeto arquivado por outra requisição. SaveChanges mantém as duas alterações na mesma transação e retorna 409 no conflito, revertendo a gravação inteira. O cliente invalida o cache de suítes e de projeto após salvar.

FK TestSuites.ProjectId → Projects.Id usa Restrict; não há DELETE. Tags são coleção serializada como JSON em coluna text, com ValueComparer para tracking; isso evita dependência de arrays específicos do PostgreSQL nesta fase. Busca atual é por nome; índice (ProjectId, CreatedAt, Id). Migração AddTestSuites e SQL idempotente completo SchemaWithTestSuites.sql. Readiness consulta ambas as tabelas e verifica migrações pendentes.

Frontend possui seleção paginada de projetos, listagem de suítes e formulário de consulta/edição/criação; projetos arquivados tornam o formulário somente leitura. HTTP compartilhado fica em api/http.ts. Status JSON são nomes de enum; valores numéricos ou desconhecidos são rejeitados. Casos e ambientes foram adicionados na versão 0.4.0.

## Execução: desenho para fases futuras
Cliente → DTO validado → caso de uso → TestRun persistido como Queued → worker reclama job atomicamente → ITestRunner.RunAsync(configuração, CancellationToken) → PlaywrightTestRunner envia JSON a um processo fixo → eventos/resultados persistidos → polling na UI. Snapshot inclui URL autorizada, parâmetros e versão do catálogo. Nenhum argumento de shell é construído a partir do cliente. Usar executable fixo e ArgumentList ou protocolo JSON; catálogo mapeia IDs de casos para testes conhecidos.

Fila inicial no PostgreSQL, com lease, heartbeat e retomada de jobs abandonados; resultados idempotentes por execução/tentativa. Não usar Task.Run sem supervisão nem fila exclusivamente em memória. Cancelamento persiste intenção e interrompe árvore de processos. Timeout é limite global da execução, com orçamento restante propagado ao runner.

Estados: Pending → Queued → Running → Passed/Failed/Error; Pending/Queued/Running → Cancelled. Estados terminais não reabrem. Transição para terminal usa controle de concorrência para impedir corrida entre cancelamento e conclusão. Failed significa testes reprovados; Error significa falha do runner/infraestrutura. Sucesso = passed / (passed + failed) × 100; sem testes avaliados retorna 0. Skipped fica fora do denominador. Retries preservam tentativas; contadores agregam resultado final por caso e navegador.

## Riscos e mitigação
- Código de testes executa com privilégios do worker: aceitar somente catálogo mantido e confiável, sem uploads arbitrários; isolamento e limites de CPU/memória/processos antes do runner real.
- URLs de ambientes podem acessar serviços internos: whitelist administrada e restrição de egress; segredo vem de configuração do worker, nunca da UI ou do snapshot.
- Production pode alterar dados: bloqueada até autorização e política explícita de testes seguros.
- Reinício e concorrência: fila persistente, lease, idempotência e transições atômicas.
- Evidências podem conter dados sensíveis: paths gerados no servidor, downloads autorizados, sanitização de logs e retenção configurável. Não publicar diretório de artefatos estaticamente.
- Headed em Linux exige display virtual e consome mais recursos; não prometer janela interativa remota.
- Drift do catálogo: armazenar chave estável/versão e marcar casos removidos, preservando histórico.
- Volume de resultados: paginação e índices desde a feature de histórico; não carregar artefatos binários no banco.
- Ambiente atual sem Docker e inicialmente sem SDK: SDK .NET instalado localmente em `.tools/dotnet` para validação. Compose deve ser validado em host com Docker antes do aceite de infraestrutura.

## Referências verificadas
- [OpenAPI nativo ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [Health checks ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [Requisitos do Vite](https://vite.dev/guide/)
- [Concorrência otimista no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [Aplicação de migrações](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Limitações dos substitutos de banco em testes](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy)


## Casos e ambientes
CatalogService/ICatalogStore mantêm as operações do catálogo fora dos endpoints. CatalogMetadata compartilha validação de nome, descrição e tags entre suítes e casos. TestCase preserva StableKey/TestSuiteId; cada edição incrementa CatalogVersion e renova Version. ProjectEnvironment preserva ProjectId/Name. Ambos usam token de concorrência EF e atualizam a versão do projeto na mesma transação. Assim, arquivamento concorrente reverte toda escrita. Índices únicos protegem chaves/tipos inclusive em concorrência; PostgreSQL unique violation é traduzida em 409.

Frontend em features/catalog, com listagem paginada de casos e formulários locais. Campos são preservados em erro; cancelar/recarregar descarta a edição e busca versões atuais. Cache de catálogo e projetos é invalidado ao salvar. A API valida URLs mas não as acessa. Sem execução ou atribuição automática de testes executáveis.

Migração AddCasesAndEnvironments e SQL SchemaWithCatalog.sql. Readiness exige todas as quatro tabelas e nenhuma migração pendente. Validação Docker/PostgreSQL adiada para o fim por decisão do usuário.


## Execuções — implementação 0.5.0
TestRunService valida vínculos e estado de projeto/suíte/ambiente/casos no servidor; cliente envia apenas IDs, tags e opções estruturadas. RunOptions valida enums e limites. Snapshot JSON schemaVersion=1 contém os metadados lidos do banco e opções; é persistido em text e não possui endpoint de edição. DTO expõe objeto estruturado, datas, versão e runnerAvailable=false. CatalogTags centraliza a normalização de tags compartilhada.

Criação atualiza o token do projeto na mesma transação que insere TestRun: todas as alterações do catálogo também tocam esse token, portanto uma alteração concorrente impede salvar configuração com leitura desatualizada. Estado de execução usa token próprio. Cancelamento é idempotente quando já Cancelled e, nos demais estados, exige versão atual; conflito entre conclusão/cancelamento resulta em 409 com rollback. Pending é o estado inicial nesta entrega. Transições Queue/Start/Complete são métodos de domínio, sem endpoints públicos. Estados terminais não reabrem. Cancelamento físico de processo e intenção persistente para worker serão responsabilidade da próxima etapa.

Frontend features/runs mantém formulário durante falhas, revisão antes de salvar, histórico paginado, consulta de snapshot e confirmação de cancelamento; erro de concorrência permite recarregar. Sem polling de progresso fictício. Migração AddTestRuns; índices por projeto/data/ID e status/data/ID, FKs Restrict e readiness na quinta tabela. Não inicia testes nem converte pendências em fila automaticamente.


## Runner — implementação 0.6.0
IRunnerPolicy valida catálogo fixo/origens/tipos; RunQueueService compara versões da configuração salva com catálogo atual e atualiza token do projeto na mesma transação do enqueue. ITestRunner separa Application do adapter PlaywrightTestRunner. API e worker usam o mesmo binário, com `--worker` iniciando somente o loop consumidor. Não há Task.Run para jobs; o loop supervisionado usa fila TestRuns no banco.

RunWorker cria DbContexts por operação. Claim otimista muda Queued para Running e grava LeaseId/LeaseExpiresAt; somente o vencedor do token inicia processo. Heartbeats/eventos são serializados por instância e conferem lease/estado/expiração. Tentativas até 2000 eventos são persistidas como JSON; result precisa conter exatamente o total de casos × browsers selecionados. Erros de protocolo/browser/timeout terminam Error; assertion failures terminam Failed. Cancelamento persiste intenção e o adapter mata a árvore antes de finalizar. Se não puder finalizar por queda de banco, recuperação de lease expirado conclui Error/Cancelled sem replay.

Node recebe apenas snapshot/origens por stdin. Executável e arquivos são configurados pelo mantenedor; ProcessStartInfo.ArgumentList e spawn(shell:false) não interpolam comandos do cliente. Capturas em diretório UUID fora do webroot. Progress não contém mensagens cruas de erro ou URLs. A fila depende de PostgreSQL em uso real; SQLite apenas comprova o fluxo em testes. Concorrência/recuperação no PostgreSQL e isolamento de recursos/rede seguem para a validação final. Referências e limitações de controle de rede em automation/README.md.
