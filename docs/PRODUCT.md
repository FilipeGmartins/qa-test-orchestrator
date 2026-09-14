# QA Test Orchestrator — Produto

## Visão e personas
Uma plataforma web para que testers configurem, executem e investiguem testes sem conhecer comandos do Playwright. O tester seleciona configurações; o líder de QA consulta resultados e tendências; o mantenedor cadastra suítes e mantém a integração de automação.

## MVP definido
Um fluxo vertical funcional: cadastrar e arquivar projetos; cadastrar suítes e casos vinculados a um catálogo de testes confiável; escolher projeto, suíte/casos, tipo, ambiente e navegador; revisar a configuração em cinco etapas; executar Playwright em worker; acompanhar estado e progresso; cancelar; consultar histórico filtrável; inspecionar falhas e evidências.

Tipos iniciais: Smoke, Regression, End-to-End, API e Accessibility. Esses tipos classificam testes existentes: selecionar Accessibility não cria automaticamente auditorias de acessibilidade. Ambientes cadastrados por projeto, com URLs aprovadas pelo administrador. Chromium, Firefox, WebKit e Todos; execução headless e headed (esta última requer display no worker).

Limites: workers 1–10, retries 0–5, timeout 5–300 segundos por execução; cada evidência usa Always, OnFailure ou Never. Tags seguem padrão validado. A tela de resumo mostra todos os valores antes da confirmação. Configuração fica imutável após enfileirar.

## Primeira entrega — Fundação
Estrutura de camadas, frontend React com estado real de conectividade, API de diagnóstico, configuração EF Core/PostgreSQL, Docker Compose local, OpenAPI, tratamento centralizado de erros e testes da fundação. Não inclui CRUD, execuções ou métricas fictícias. Aceite: builds e testes passam; API diferencia processo vivo de banco acessível; frontend mostra carregamento, indisponibilidade e resposta; ambiente Compose documentado.

## Backlog por prioridade
### Segunda entrega — Projetos
Implementados cadastro, consulta por ID, edição de ativos, arquivamento lógico idempotente, busca por nome sem distinção de maiúsculas/minúsculas, filtros active/archived/all e paginação. Nome obrigatório de 1–120 caracteres após trim; descrição opcional de até 2000. Nomes duplicados são permitidos; UUID identifica o projeto. Edição e arquivamento exigem versão atual para evitar perda de alterações concorrentes. Projetos arquivados ficam disponíveis para consulta; reativação e exclusão física estão fora desta entrega. Suítes e execuções relacionadas entrarão nas respectivas fases.

Aceite funcional validado com testes locais. Aceite PostgreSQL/Compose depende do Docker em instalação; não substituir o banco da aplicação por armazenamento simulado.

### Terceira entrega — Suítes (parte 1 do catálogo)
Cadastro, consulta, edição, ativação/inativação e listagem paginada de suítes por projeto. Nome de 1–120 caracteres, descrição opcional até 2000, até 20 tags com padrão @ seguido de 1–40 letras ASCII/números/hífen/sublinhado. Tags normalizadas para minúsculas e deduplicadas. Não há exclusão física ou transferência de suíte para outro projeto. Projetos arquivados permitem consulta, mas não alteração do catálogo.

Casos de teste, ambientes e integração com catálogo executável continuam pendentes, para não antecipar funcionalidades. A próxima entrega completa essa parte do catálogo antes de iniciar execuções.

### P0 — Caminho essencial, em ordem de dependência
1. Fundação: repositório, documentação, frontend/API, banco, Compose e testes básicos.
2. Projetos (implementado; validação PostgreSQL pendente): criar, listar, editar, arquivar; validar nomes; paginação; testes API e formulário. Arquivamento preserva histórico.
3. Suítes/casos/ambientes: CRUD e catálogo confiável, integridade de vínculos, seleção de casos e tags, bloqueio de suítes inativas.
4. Execuções: domínio e transições, validação estruturada, wizard, histórico, cancelamento e snapshot da configuração; testes de concorrência.
5. Runner real: ITestRunner, adapter Playwright, fila persistida, limites, resultados incrementais, recuperação de worker e deduplicação.
6. Resultados/evidências: detalhes, histórico por caso, screenshot/vídeo/trace/log, download autorizado e retenção; E2E criar → acompanhar → resultado → filtrar.
7. Acesso mínimo antes de disponibilização compartilhada: autenticação, autoria confiável, autorização para executar/cancelar e acessar evidências. Production fica desabilitado até existir autorização específica.

### P1 — Evolução após o fluxo essencial
8. Dashboard: totais de execuções e testes, aprovados/falhos/ignorados, sucesso, média de duração, evolução, falhas recentes e instabilidade; Recharts somente nesta fase.
9. Presets por projeto, incluindo Smoke Production, Regression Full e Debug Login.
10. Roles e permissões detalhadas; CI/CD com GitHub Actions e ambientes protegidos.

### P2 — Fora do MVP
Múltiplos frameworks, execução distribuída/elástica, edição/upload arbitrário de código, clonagem de repositórios informados pela UI, agendamento recorrente, multi-tenancy, notificações, análise por IA, comparações avançadas de flakiness e armazenamento remoto de artefatos. Não antecipar essas implementações.

## Roadmap e situação
As dez fases do briefing são mantidas: Fundação → Projetos → Suítes → Execuções → Playwright → Dashboard → Artefatos → Presets → Autenticação → CI/CD. A entrega utilizável do MVP exige artefatos e controle mínimo de acesso; a ordem de liberação os antecipa em relação ao dashboard e aos presets. Fundação, Projetos e cadastro de Suítes implementados; próximos itens: Casos de Teste e Ambientes. Aceite de infraestrutura depende de iniciar o engine Docker após reiniciar o Windows e executar Compose/PostgreSQL.

## Critérios do produto
Nenhum comando shell vindo do cliente. Nenhuma execução simulada apresentada como real. Estado terminal consistente, falha de infraestrutura separada de falha de teste, mensagens úteis sem segredos e acessibilidade por teclado. Os testes de criação, resultado, filtros, status e cálculo de sucesso serão acrescentados junto às respectivas features.
