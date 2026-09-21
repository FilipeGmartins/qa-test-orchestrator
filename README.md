# QA Test Orchestrator

Plataforma para configurar e acompanhar testes automatizados sem comandos do framework.

**Entrega atual: Autenticação, autoria e permissões (v0.10.0).** Login obrigatório, perfis Administrador/Operador/Leitor, usuários, troca de senha, revogação de sessões e autoria das execuções. Veja [primeiro acesso e segurança](docs/AUTHENTICATION.md). Presets, dashboard, catálogo, runner e evidências já implementados. Próximas etapas: CI/CD e Docker/PostgreSQL (último); aceite completo de infraestrutura ainda pendente.

## Executar com Docker

Requisito: Docker Engine/Desktop com Compose v2 e suporte a containers Linux.

Se a API de desenvolvimento estiver rodando na porta 5080, encerre-a antes de iniciar o Compose para evitar conflito de porta. O frontend Vite em 5173 pode permanecer aberto.

```sh
docker compose up --build
```

Abra http://localhost:8080. API: http://localhost:5080/api/system. OpenAPI: http://localhost:5080/openapi/v1.json. O Compose usa configuração **local**, porta restrita a loopback e senha de desenvolvimento. Opcionalmente copie `.env.example` para `.env` antes de criar o volume. Não publicar esta configuração na internet.

Compose inicia PostgreSQL, executa o job `migrate` (InitialProjects) e só depois inicia a API. Se a migração falhar, verifique `docker compose logs migrate`. `docker compose down` encerra os serviços e preserva o banco; não use `-v` para preservar os projetos. Nesta máquina Docker já está instalado; os recursos Plataforma de Máquina Virtual e WSL foram habilitados e aguardam reinicialização do Windows. Containers e PostgreSQL real ainda precisam ser validados após o engine iniciar.

## Desenvolvimento sem containers

Requisitos: Node.js 24, SDK .NET 10 e PostgreSQL 17. O SDK é selecionado por `global.json`. Nesta máquina foi baixado um SDK em `.tools/dotnet`; ele não é versionado.

Terminal 1 (PowerShell, raiz do repositório):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/src/Api -- --migrate
./scripts/bootstrap-admin.ps1 # Somente no primeiro acesso ao banco migrado.
dotnet run --project backend/src/Api --urls http://127.0.0.1:5080
```

Se não houver SDK global, substitua `dotnet` por `& .\.tools\dotnet\dotnet.exe`. Para usar os pacotes locais restaurados nesta máquina, defina `$env:NUGET_PACKAGES = Join-Path $PWD '.cache\nuget'` e `$env:DOTNET_CLI_HOME = Join-Path $PWD '.cache\dotnet-home'`.

Terminal 2:

```sh
cd frontend
npm ci
npm run dev
```

Abra http://localhost:5173/projects. O Vite encaminha `/api` e `/openapi` ao backend. Configure `ConnectionStrings__Database` para usar outro banco; os valores locais padrão estão em `appsettings.Development.json`. Sem PostgreSQL, a API continua acessível e a interface informa banco indisponível; cadastros não são gravados até o banco e a migração estarem prontos. Em ambiente diferente de Development, a connection string deve ser fornecida e OpenAPI não é exposto.

## Validar

```sh
dotnet test backend/QaTestOrchestrator.slnx
cd frontend
npm test
npm run build
npx playwright install chromium
npm run test:e2e
```

Os testes HTTP de Projetos usam SQLite relacional isolado; os testes de navegador usam respostas controladas. Eles não substituem PostgreSQL real. A suíte específica de PostgreSQL fica ignorada sem `QA_TEST_DATABASE`. O roteiro para executá-la está em [TESTING.md](docs/TESTING.md).

Resultado local: **32 testes backend + 19 frontend + 5 navegador aprovados; 1 teste PostgreSQL pendente**. Builds aprovados. As migrações de Projetos e Suítes e o [SQL idempotente completo](docs/migrations/SchemaWithTestSuites.sql) estão incluídos na entrega. Acesse `/test-suites` ou o botão de suítes no detalhe de um projeto.

## Próxima entrega

Quando Docker estiver pronto, validar Compose e a suíte PostgreSQL. Próxima entrega de produto: Casos de Teste e Ambientes, completando o catálogo antes de iniciar execuções. Antes de começar, ler `docs/PRODUCT.md` e `docs/ARCHITECTURE.md`; após concluir, atualizar testes e changelog. Não antecipar as fases futuras.


## Acompanhamento no GitHub
- [Repositório privado](https://github.com/FilipeGmartins/qa-test-orchestrator)
- [Roadmap Kanban](https://github.com/users/FilipeGmartins/projects/1)
- [Etapas e critérios de aceite](https://github.com/FilipeGmartins/qa-test-orchestrator/issues)

### Catálogo — v0.4.0
Abra um projeto → **Ambientes** para configurar Development, Staging e Production. Abra uma suíte → **Ver casos de teste** para cadastrar e editar casos. Chaves permanentes são únicas por suíte; casos podem ser inativados. Production permanece desabilitado. Esta entrega cadastra metadados: a ligação com código Playwright entra na etapa do runner.

Por decisão do usuário, a integração Docker/PostgreSQL e o aceite de infraestrutura ficam para a última etapa (#10). As funcionalidades são verificadas com testes locais sem substituir PostgreSQL como banco da aplicação. A migração mais recente é AddCasesAndEnvironments; SQL completo em `docs/migrations/SchemaWithCatalog.sql`.


### Execuções — v0.5.0
Abra um projeto → **Histórico de execuções** → **Nova execução**. O assistente possui cinco etapas: suíte, casos/tags, ambiente, opções e revisão. A confirmação salva uma solicitação **Pendente** com configuração imutável; não dispara testes. Consulte o histórico geral pela navegação **Execuções** ou o histórico de um projeto, filtre por estado e abra os detalhes para cancelar.

O runner será implementado na próxima entrega (#4); não existe worker nem enfileiramento público nesta versão. Solicitações pendentes não devem ser disparadas automaticamente na futura ativação do runner: será necessária ação explícita e revalidação. Nova migração: AddTestRuns; SQL completo `docs/migrations/SchemaWithTestRuns.sql`. Docker/PostgreSQL permanece para a etapa final.


### Runner — v0.6.0
Runner real implementado, com fila persistente, lease/heartbeat, progresso, retries, timeout global e cancelamento do processo. Consulte [configuração e catálogo executável](automation/README.md). Está desabilitado por padrão; deve ser habilitado explicitamente no servidor, com origens aprovadas e worker separado. A aplicação não transforma metadados em código de teste. Testes reais do adapter foram executados com Chromium e servidor local; PostgreSQL/Docker real permanece reservado para a etapa final.
