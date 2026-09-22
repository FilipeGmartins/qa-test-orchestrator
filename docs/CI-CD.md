# Integração contínua sem Docker

O workflow `.github/workflows/ci.yml` executa em pushes para `main`, pull requests e por acionamento manual na aba Actions. Usa Ubuntu 24.04, Node.js 24, .NET 10 e Chromium instalados diretamente no runner do GitHub. Não exige Docker na máquina do desenvolvedor, serviços de banco ou segredos de implantação.

## Verificações

- **Frontend:** `npm ci`, testes Vitest, build TypeScript/Vite e E2E Playwright com API controlada. `--forbid-only` impede publicar testes focados acidentalmente.
- **Backend and runner:** restore NuGet com lock files, build Release, testes .NET com `QA_RUNNER_INTEGRATION=1` e testes Node do executor. A integração usa SQLite isolado, servidor de teste local e Chromium real.
- EF Core verifica mudanças de modelo sem migração e gera SQL PostgreSQL idempotente, sem abrir conexão com banco. A ferramenta `dotnet-ef` 10.0.12 está fixada em `.config/dotnet-tools.json`; use `dotnet tool restore` antes dos comandos locais.

PostgreSQL permanece ignorado sem `QA_TEST_DATABASE`. Os E2E da interface usam respostas controladas e não comprovam a operação completa com PostgreSQL. Esse aceite pertence à etapa #10.

## Resultados e arquivos compilados

Abra a execução na [aba Actions](https://github.com/FilipeGmartins/qa-test-orchestrator/actions/workflows/ci.yml). Os relatórios TRX e HTML/traces do Playwright são enviados mesmo quando testes falham, desde que a execução não seja cancelada e os arquivos existam. Artefatos ficam disponíveis por sete dias.

Cada job aprovado disponibiliza seu build identificado pelo SHA: `frontend-<sha>` contém os arquivos estáticos e `backend-<sha>` contém a API publicada e `migrations.sql`. Só considere o conjunto validado quando **ambos** os jobs passarem. O pacote da API não é um worker autossuficiente: para executar testes também são necessários o catálogo `automation`, Node, dependências Playwright e navegadores.

## Permissões e entrega

Actions são fixadas por SHA, checkout não persiste credenciais, token tem somente leitura do repositório e nenhum segredo é fornecido aos testes. Novas execuções da mesma branch substituem as anteriores. Jobs têm limites de tempo explícitos.

Esta entrega implementa CI e disponibilização de builds, **não deploy automático**. Destino de hospedagem, credenciais e ambiente protegido de implantação ainda precisam ser definidos e validados na etapa final. Nenhum ambiente de produção ou regra de proteção foi criado por este workflow.

Após validar a primeira execução, os checks `Frontend` e `Backend and runner` podem ser exigidos na proteção da branch `main`, conforme recursos disponíveis no plano do repositório. Essa configuração é feita no GitHub e não é aplicada automaticamente pelo arquivo YAML.

Referências oficiais: [setup-dotnet](https://github.com/actions/setup-dotnet), [setup-node](https://github.com/actions/setup-node), [upload-artifact](https://github.com/actions/upload-artifact).
