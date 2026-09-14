# Arquitetura

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

Frontend possui seleção paginada de projetos, listagem de suítes e formulário de consulta/edição/criação; projetos arquivados tornam o formulário somente leitura. HTTP compartilhado fica em api/http.ts. Status JSON são nomes de enum; valores numéricos ou desconhecidos são rejeitados. Casos e ambientes serão implementados separadamente.

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
