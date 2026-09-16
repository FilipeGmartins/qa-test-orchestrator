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

Casos e ambientes implementados na entrega 0.4.0 descrita abaixo. A ligação com código executável continua reservada à etapa do runner.

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
As dez fases do briefing são mantidas: Fundação → Projetos → Suítes → Execuções → Playwright → Dashboard → Artefatos → Presets → Autenticação → CI/CD. A entrega utilizável do MVP exige artefatos e controle mínimo de acesso; a ordem de liberação os antecipa em relação ao dashboard e aos presets. Fundação, Projetos e cadastro de Suítes implementados; Casos de Teste e Ambientes implementados; Configuração e Histórico de Execuções implementados; Runner Playwright implementado com catálogo inicial; Resultados e Evidências implementados; próximo item: Dashboard e métricas reais. Aceite de infraestrutura depende de iniciar o engine Docker após reiniciar o Windows e executar Compose/PostgreSQL.

## Sétima entrega — Resultados e Evidências (0.7.0)
Detalhes por tentativa com navegador, número do retry, status, duração, mensagem de erro, stack e logs do processo de teste. Filtros por status/navegador e paginação de 12 itens; histórico por caso acessível pelo catálogo e pelos resultados. Contadores finais continuam separados das tentativas para não contar retries como testes adicionais.

Screenshot, vídeo e trace podem ser baixados por identificador. Os arquivos são associados à execução/tentativa e expiram após 14 dias por padrão. A limpeza preserva os registros de resultados, mesmo quando os binários já não existem. Downloads não expõem caminhos internos e não usam diretório estático. Não há autorização por usuário ainda: acesso local, autenticação/permissões na etapa #8. Capturas podem conter dados sensíveis e não recebem redação visual automática.

Etapa #5 implementada. Próximas entregas: dashboard/métricas, presets e autenticação; Docker/PostgreSQL continua por último. Execuções anteriores à versão 0.7 não têm detalhes retroativos inventados.

## Critérios do produto
Nenhum comando shell vindo do cliente. Nenhuma execução simulada apresentada como real. Estado terminal consistente, falha de infraestrutura separada de falha de teste, mensagens úteis sem segredos e acessibilidade por teclado. Os testes de criação, resultado, filtros, status e cálculo de sucesso serão acrescentados junto às respectivas features.


## Quarta entrega — Casos e Ambientes (0.4.0)
Casos vinculados a uma suíte, com nome, descrição, tags, status Active/Inactive, chave permanente e revisão incremental. Chave normalizada para minúsculas, 1–80 caracteres ASCII alfanuméricos/hífen/sublinhado, única por suíte e imutável após criar. Busca por nome/chave, filtro de status e paginação. Não aceita código ou caminho executável. Suíte inativa permite manutenção do catálogo; execuções futuras deverão bloquear suítes inativas. Projeto arquivado bloqueia toda escrita, preservando consulta.

Ambientes únicos por projeto e tipo (Development/Staging/Production), URL base HTTP(S) de até 2048 caracteres sem credenciais, query ou fragmento. Desabilitados por padrão; Production não pode ser habilitado nesta fase. Nenhuma requisição é feita para a URL cadastrada. Cadastro local não equivale a autorização de egress: controles do runner e autorização administrativa seguem pendentes.

[Roadmap no GitHub](https://github.com/users/FilipeGmartins/projects/1). Docker/PostgreSQL fica explicitamente por último (#10), conforme decisão do usuário. Aceite local desta entrega não representa validação PostgreSQL.


## Quinta entrega — Configuração e Histórico de Execuções (0.5.0)
Assistente de cinco etapas por projeto: escolher suíte ativa, selecionar 1–100 casos ativos e tags opcionais, escolher ambiente habilitado fora de Production, configurar parâmetros/evidências e revisar. Tags usam semântica OR: cada caso selecionado precisa ter ao menos uma tag solicitada; sem tags, todos os casos selecionados são incluídos. A seleção é explícita, com paginação e até 100 casos por solicitação. Tipo classifica testes e não cria automaticamente testes novos.

Salvar cria Pending, preservando cópia do nome de projeto/suíte, URL/tipo/versão de ambiente, chaves/revisões/tags dos casos e todas as opções. Nenhum campo é editável após salvar; para outra configuração, criar nova solicitação. Histórico geral e por projeto com filtro de estado e paginação; detalhe e cancelamento. Projeto arquivado preserva consulta/cancelamento de solicitações existentes, mas impede novas.

Runner indisponível é informado no produto. Não há endpoint para enfileirar, iniciar ou concluir; essas transições existem apenas no domínio com testes. A próxima etapa deverá exigir confirmação explícita e revalidar catálogo/permissões antes de enfileirar pendências, além de integrar cancelamento do processo. Nenhum resultado simulado ou métrica inventada foi adicionado.


## Sexta entrega — Runner real (0.6.0)
Confirmação explícita no detalhe chama enqueue, revalida versões/estado do catálogo e origem aprovada. Sucesso não é simulado: o worker separado invoca Playwright CLI real. Catálogo inicial: page-title e http-ok, com tipos suportados publicados em GET /api/runner; outros casos/tipos são rejeitados ao enfileirar. Runner desabilitado por padrão e Production bloqueado. Autoria/permissões ainda pendentes; uso local.

Progresso mostra tentativas reais, incluindo retries. Resumo final informa aprovados/falhos/ignorados por caso/navegador. Cancelamento em Running exibe intenção até o processo terminar. Lease expirado encerra Error sem replay automático. Interface atualiza estados ativos a cada dois segundos. Habilitação no servidor não significa worker online: fila aguarda worker. Evidências já são capturadas conforme política, mas detalhes de falhas, downloads autorizados e retenção serão a próxima entrega.
