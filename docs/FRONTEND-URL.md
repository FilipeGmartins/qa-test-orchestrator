# Testar página por URL — v0.11.0

## Usar na ferramenta
1. Entre como Administrador ou Operador e abra **Testar página por URL** no menu.
2. Escolha projeto ativo e ambiente habilitado (fora de Production).
3. Informe a URL final HTTP/HTTPS, na mesma origem do ambiente, sem usuário/senha, query string ou fragmento.
4. Escolha verificações e tamanhos de tela, revise e salve como pendente.
5. Nos detalhes, confirme **Executar agora**. Consulte os resultados por caso e baixe as capturas.

Salvar não inicia navegação nem testes. O worker deve estar habilitado e em execução; habilitar a API não inicia o worker. Projeto, ambiente, casos e origens são revalidados antes da fila e o worker verifica a política de origem novamente. A autoria usa a sessão existente; Leitor consulta os resultados sem poder criar/enfileirar/cancelar.

## Verificações
- **Carregamento:** navegação com HTTP 2xx; registra falhas de requisição e recursos com HTTP 4xx/5xx durante a observação.
- **Erros JavaScript e console:** captura pageerror e console.error. Mensagens e listas são limitadas, sanitizadas pelo pipeline de resultados existente.
- **Responsividade:** compara a largura do documento com a tela, com tolerância de 1 pixel. Não detecta todo problema visual, sobreposição ou diferenças em relação a um desenho de referência.

Cada seleção é combinada com Desktop (1440×900), Tablet (768×1024) e/ou Celular (390×844). São tamanhos de viewport em Chromium, não emulação completa de aparelhos reais. Cada combinação usa contexto isolado e gera um resultado e screenshot, até nove verificações. Observação de 1,5 segundo após DOMContentLoaded; conteúdo tardio pode ficar fora da janela. Navegação com limite de 30 s; execução total de 120 s, um worker, sem retries, screenshot Always, vídeo Never e trace OnFailure.

## Domínios e rede
As origens exatas (protocolo + host + porta) precisam constar em Runner:AllowedOrigins, configurado pelo mantenedor nos processos API e worker. Exemplo de configuração local, após aprovar o alvo:

```powershell
$env:Runner__Enabled = 'true'
$env:Runner__AllowedOrigins__0 = 'https://staging.exemplo.com'
# Se a página usa recursos de uma CDN aprovada, configure outra entrada:
$env:Runner__AllowedOrigins__1 = 'https://cdn.exemplo.com'
```

Não existe liberação automática de domínios na interface. Falha de recurso bloqueado aparece como falha de carregamento; revise a política com o administrador. Requisições diferentes de GET/HEAD são bloqueadas nos casos frontend, assim como WebSockets e Service Workers. GETs também podem ter efeitos no servidor; use alvos e dados de teste autorizados.

Redirecionamentos HTTP com Location são bloqueados no runner de navegador, inclusive os casos antigos page-title. A rota Playwright só intercepta o primeiro URL de uma cadeia HTTP, então devolver um redirect ao browser permitiria escapar da validação dos próximos destinos. Informe diretamente a URL final. Cada resposta é buscada sem seguir redirects e rejeitada antes de ser entregue ao browser se contiver redirecionamento. Teste controlado comprova que o servidor de destino não recebe requisição.

A lista de origens é uma restrição de aplicação, não um sandbox completo de rede/processo: proteção contra DNS rebinding, WebRTC e outros canais depende do isolamento de rede do worker, previsto para o aceite de infraestrutura. Esta etapa é destinada a páginas confiáveis autorizadas, em ambiente local. Production permanece bloqueado.

## Histórico e dados
A primeira configuração válida cria, na mesma transação da execução, uma suíte identificada internamente como Frontend por URL e nove casos confiáveis. Índice único parcial garante uma suíte desse tipo por projeto; próximas configurações a reutilizam. Não altera a URL do ambiente existente. Inativar essa suíte ou casos impede usá-los; o serviço não reativa registros automaticamente.

A URL específica fica no snapshot imutável pageUrl, separada da baseUrl do ambiente. Não requer alteração nas execuções antigas. Resultados, duração, falhas, autoria, capturas, retenção e dashboard reutilizam o pipeline existente. Migração AddPageAudits acrescenta IsPageAudit à suíte e o índice único, sem cadastrar alvos automaticamente.

Não inclui auditoria de acessibilidade, comparação visual com baseline, navegação autenticada no site-alvo ou passos de login/cadastro/compra. O acesso ao Orchestrator não compartilha seus cookies com o navegador do teste. Integração PostgreSQL/Docker permanece na etapa final.

Referências: [Page e eventos](https://playwright.dev/docs/api/class-page), [Route.fetch](https://playwright.dev/docs/api/class-route#route-fetch) e [Network](https://playwright.dev/docs/network).
