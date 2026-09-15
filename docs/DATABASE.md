# Modelo de dados

PostgreSQL, UUIDs, datas UTC/timestamptz, FKs explícitas. Projects, TestSuites, TestCases e ProjectEnvironments implementadas; as entidades de execução continuam planejadas. Não usar EnsureCreated como substituto de migrações na aplicação.

- User: Id, ExternalSubject único, DisplayName, Active. Autoria será resolvida pela identidade autenticada.
- Project (implementado): Id uuid PK, Name varchar(120) obrigatório, Description varchar(2000) obrigatório com string vazia quando omitido, ArchivedAt timestamptz nullable, CreatedAt/UpdatedAt timestamptz e Version uuid obrigatório/token de concorrência. Índices (CreatedAt, Id) e (ArchivedAt, CreatedAt, Id). Nomes duplicados permitidos. Arquivamento lógico; sem cascata destrutiva de histórico.
- TestSuite (implementada): Id uuid PK, ProjectId uuid FK Restrict, Name varchar(120), Description varchar(2000), Tags text JSON, Status varchar(16) Active/Inactive, CreatedAt/UpdatedAt timestamptz, Version uuid concorrência. Índice (ProjectId, CreatedAt, Id). Tags validadas/normalizadas no domínio.
- TestCase (implementada): Id uuid PK, TestSuiteId FK Restrict, Name varchar(120), Description varchar(2000), StableKey varchar(80), CatalogVersion int, Tags text JSON, Status varchar(16), Version uuid concorrência, CreatedAt/UpdatedAt UTC. Unique(TestSuiteId, StableKey), índice (TestSuiteId, CreatedAt, Id).
- Environment (implementada; tabela ProjectEnvironments, CLR ProjectEnvironment): Id, ProjectId, Name (Development/Staging/Production), BaseUrl, Enabled, Version uuid concorrência, CreatedAt/UpdatedAt UTC. FK Restrict. Unique(ProjectId, Name). Credenciais ficam fora do banco/snapshot inicial.
- TestRun: Id, ProjectId, TestSuiteId, TestType, EnvironmentId, Browser, ExecutionMode, Workers, Retries, TimeoutSeconds, Tags, EvidencePolicy, SelectedCaseIds, ConfigurationSnapshot, Status, CreatedAt, StartedAt?, FinishedAt?, TotalTests, PassedTests, FailedTests, SkippedTests, CreatedBy. SuccessRate calculado, não duplicado como coluna mutável; exposto no DTO. Duração calculada a partir dos timestamps. Concurrency token/lease entram com worker.
- TestExecution: Id, TestRunId, TestCaseId?, TestName (snapshot), Attempt, Status, DurationMs, Browser, ErrorMessage?, StackTrace?, StartedAt, FinishedAt. Unique(TestRunId, chave do caso, Browser, Attempt). Resultado final por caso/navegador determina agregados.
- TestPreset: Id, ProjectId, Name, ConfigurationJson, SchemaVersion, CreatedBy, UpdatedAt. Revalidar ao usar: catálogo/ambiente podem mudar.
- TestArtifact: Id, TestRunId, TestExecutionId, ArtifactType, FilePath (relativo à raiz controlada), CreatedAt, ContentType, SizeBytes. FK composta assegura que TestExecutionId pertença a TestRunId. Arquivos fora do banco.

## Relações e integridade
Projects 1:N TestSuites, Projects 1:N ProjectEnvironments e TestSuites 1:N TestCases implementados; os demais relacionamentos serão adicionados incrementalmente.

Project 1:N TestSuite, ProjectEnvironment, TestRun e TestPreset; TestSuite 1:N TestCase; TestRun 1:N TestExecution e TestArtifact; TestExecution 1:N TestArtifact. Validar e restringir que suíte, ambiente e casos pertençam ao projeto informado; usar chaves compostas quando necessário. Preservar snapshots quando o catálogo for alterado.

## Índices previstos
TestRun(ProjectId, CreatedAt DESC), TestRun(Status, CreatedAt), TestExecution(TestRunId), TestExecution(TestCaseId, FinishedAt DESC), TestArtifact(TestRunId), TestSuite(ProjectId). Listagens paginadas. Políticas de retenção de execução e artefato serão definidas antes da fase de evidências.

## Enums e validação
TestRunStatus: Pending, Queued, Running, Passed, Failed, Cancelled, Error. TestType, Browser, ExecutionMode, CapturePolicy, ArtifactType e TestExecutionStatus centralizados no domínio quando implementados. Serialização JSON como nomes; reject de valores desconhecidos. Tipos de teste terão catálogo/mapeamento para extensão sem ramificações nos controllers.

## Aplicar schema
Compose executa job de migração automaticamente antes da API. Fora do Compose: `dotnet run --project backend/src/Api -- --migrate`, com ambiente/connection string configurados. Script alternativo revisável: `docs/migrations/InitialProjects.sql` (idempotente, gerado pelo provider PostgreSQL). Não aplicar Down em dados que precisam ser preservados: ele remove a tabela Projects. Após a migração, readiness deve responder 200.

Na versão 0.3.0, use `docs/migrations/SchemaWithTestSuites.sql` para o schema completo e incremental. InitialProjects.sql foi preservado como referência da versão anterior.

Na versão 0.4.0, use `docs/migrations/SchemaWithCatalog.sql` para todas as migrações. AddCasesAndEnvironments cria as novas tabelas sem alterar dados existentes.
