# Autenticação — v0.10.0

## Primeiro acesso
A API não possui cadastro público, credencial padrão nem usuário criado automaticamente. Aplique as migrações no banco configurado e crie o primeiro administrador uma única vez:

```powershell
# Na raiz, com DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES conforme README.
& .tools/dotnet/dotnet.exe run --project backend/src/Api -- --migrate
./scripts/bootstrap-admin.ps1
```

O script solicita login, nome e senha sem ecoar a senha, passa os valores apenas ao processo de bootstrap e restaura as variáveis ao terminar. Não coloque senha em argumentos de linha de comando, arquivos versionados ou mensagens. A senha deve ter 12–128 caracteres. Login usa letras minúsculas, números e . _ @ + -, com 3–80 caracteres. Depois abra a interface e entre com a conta criada. Administração de contas fica em **Usuários**; troca da própria senha e saída ficam em **Minha conta**.

O comando `--bootstrap-admin` lê QA_ADMIN_LOGIN, QA_ADMIN_NAME e QA_ADMIN_PASSWORD e encerra sem iniciar servidor/worker. Ele recusa inicialização se houver qualquer conta. Banco deve estar migrado; não há fallback para usuários em memória. A ativação em PostgreSQL e a persistência entre reinícios serão validadas na etapa Docker/PostgreSQL #10, conforme a ordem combinada.

## Permissões do workspace
- **Admin / Administrador:** consulta e modifica catálogo, presets e execuções; gerencia usuários, perfis, ativação e redefinição de senha; acessa OpenAPI no ambiente Development.
- **Operator / Operador:** consulta todo o workspace, altera projetos/catálogo/presets, cria/enfileira/cancela execuções e consulta/baixa evidências. Não gerencia usuários.
- **Reader / Leitor:** consulta todo o workspace, resultados e evidências; não altera catálogo ou execuções. Pode alterar sua própria senha e sair.

São perfis globais, não segregação por projeto ou organização. Usuários desativados não entram. Alterar conta/perfil/senha renova a versão de segurança e invalida todas as sessões dessa conta na próxima requisição. O último administrador ativo não pode ser desativado/rebaixado; um registro de concorrência único protege alterações administrativas simultâneas. Não existe exclusão de usuários. Nome e ID do autor de criação, enqueue e cancelamento ficam na execução como histórico; registros antigos continuam sem autoria, sem atribuição inventada.

## Sessão e proteção HTTP
Cookie ASP.NET Core criptografado/autenticado, HttpOnly, SameSite=Strict, sessão absoluta de 8 h sem renovação deslizante. Cookie de sessão do navegador, sem token em localStorage. Sessão também existe no banco, vinculada à versão do usuário; cada requisição confere ativação, versão e expiração. Logout remove o registro, bloqueando replay do cookie. Sessões expiradas são removidas ao fazer login.

Fora de Development, cookies exigem HTTPS. O ambiente Development permite HTTP local. Não usar Development para exposição pública. O padrão do ASP.NET Core Data Protection guarda as chaves conforme o host: em produção, planejar armazenamento persistente protegido, permissões e chaveamento entre réplicas. Sem chave persistente, reinícios podem invalidar cookies. TLS/proxy e persistência dessas chaves entram no aceite de infraestrutura #10; o Compose HTTP existente não foi alterado para contornar Secure.

Toda escrita em /api, incluindo login/logout/troca de senha, exige cookie antiforgery e header X-CSRF-Token obtido em GET /api/auth/csrf. O frontend obtém token antes de cada escrita, sem repetir automaticamente operações após erros. Nova identidade exige novo token. API devolve 401/403 JSON, sem redirecionar para HTML. Consultas autenticadas usam no-store. Health/live e health/ready são públicos; demais dados, métricas, arquivos e diagnóstico exigem sessão. OpenAPI exige Admin e só existe em Development.

Senha armazenada com PasswordHasher do ASP.NET Core, salt e hash versionado. Falhas retornam mensagem genérica; cinco erros bloqueiam a conta por 15 minutos. Login/troca de senha limitados a 30 tentativas por 10 minutos por IP/instância (429); bloqueio por conta persiste no banco. Quando houver proxy/replicação, configurar IP de origem confiável e limitação distribuída/borda, sem aceitar headers encaminhados de clientes arbitrários.

## Limites desta entrega
Sem SSO/OAuth, MFA, recuperação por e-mail ou ACL por projeto. Redefinição é feita por outro administrador. Guarde acesso a uma segunda conta administrativa e backups conforme a operação do ambiente. Production continua bloqueado no runner; autenticar não autoriza testes em produção. A interface tem E2E com respostas controladas, e HTTP/cookies/CSRF têm testes reais contra SQLite isolado. PostgreSQL e sistema completo sem mocks continuam para #10.

Referências: [cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0) e [antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
