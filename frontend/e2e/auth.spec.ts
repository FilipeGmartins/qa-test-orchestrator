import { expect, test } from '@playwright/test';
test('login, leitor sem edição e logout sem revelar conteúdo protegido', async ({ page }) => {
  let loggedIn = false;
  await page.route(url => url.pathname.startsWith('/api/'), route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/csrf') return route.fulfill({ json: { token: 'csrf-test' } });
    if (path === '/api/auth/login') {
      expect(route.request().headers()['x-csrf-token']).toBe('csrf-test');
      loggedIn = route.request().postDataJSON().password === 'a-valid-password';
      if (!loggedIn) return route.fulfill({ status: 401, json: { message: 'Login ou senha inválidos.' } });
    }
    if (path === '/api/auth/logout') { loggedIn = false; return route.fulfill({ json: { signedOut: true } }); }
    if (path === '/api/auth/me' || path === '/api/auth/login') return route.fulfill({ status: loggedIn ? 200 : 401, json: loggedIn ? { id: 'reader', login: 'reader', name: 'Leitor QA', role: 'Reader', active: true } : { message: 'Entre para continuar.' } });
    return route.fulfill({ json: { items: [{ id: 'p1', name: 'Projeto privado' }], total: 1 } });
  });
  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: 'Entrar no workspace' })).toBeVisible();
  await expect(page.getByText('Projeto privado')).toHaveCount(0);
  await page.getByLabel('Login', { exact: true }).fill('reader'); await page.getByLabel('Senha', { exact: true }).fill('incorrect');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('inválidos');
  await page.getByLabel('Senha', { exact: true }).fill('a-valid-password'); await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Projeto privado' })).toBeVisible();
  await expect(page.getByRole('link', { name: '+ Novo projeto' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Usuários', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'Minha conta' }).click(); await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Entrar no workspace' })).toBeVisible();
});

test('administrador cria usuário e altera perfil e ativação', async ({ page }) => {
  let member: any;
  await page.route(url => url.pathname.startsWith('/api/'), route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path === '/api/auth/me') return route.fulfill({ json: { id: 'admin', name: 'Admin', role: 'Admin', active: true } });
    if (path === '/api/auth/csrf') return route.fulfill({ json: { token: 'csrf' } });
    if (path.startsWith('/api/users') && request.method() !== 'GET') {
      expect(request.headers()['x-csrf-token']).toBe('csrf');
      const input = request.postDataJSON();
      if (request.method() === 'PUT') expect(input.version).toBe('v1');
      member = { ...input, id: 'u1', version: request.method() === 'PUT' ? 'v2' : 'v1' };
      delete member.password; return route.fulfill({ json: member });
    }
    return route.fulfill({ json: { items: member ? [member] : [], total: member ? 1 : 0 } });
  });
  await page.goto('/users');
  await page.getByLabel('Login do usuário').fill('qa.operator'); await page.getByLabel('Nome', { exact: true }).fill('Operador QA');
  await page.getByLabel('Senha inicial').fill('a-valid-password'); await page.getByLabel('Perfil').selectOption('Operator');
  await page.getByRole('button', { name: 'Salvar usuário' }).click(); await expect(page.getByRole('heading', { name: 'Operador QA' })).toBeVisible();
  await page.getByRole('button', { name: 'Editar qa.operator' }).click(); await page.getByLabel('Perfil').selectOption('Reader');
  await page.getByLabel('Usuário ativo').uncheck(); await page.getByRole('button', { name: 'Salvar usuário' }).click();
  await expect(page.getByText('qa.operator · Leitor · Desativado')).toBeVisible();
});
