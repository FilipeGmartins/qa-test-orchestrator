import { expect, test } from '@playwright/test';

test('cadastra caso, preserva chave ao editar e configura ambiente', async ({ page }) => {
  const project = { id: 'p1', name: 'Portal', description: '', archivedAt: null, version: 'p1', createdAt: '', updatedAt: '' };
  const suite = { id: 's1', projectId: 'p1', name: 'Smoke', description: '', tags: [], status: 'Active', version: 's1', createdAt: '', updatedAt: '' };
  let cases: Record<string, unknown>[] = [];
  let environments: Record<string, unknown>[] = [];
  await page.route(/\/api\/(?:projects|test-suites|test-cases|environments)(?:[/?]|$)/, route => {
    const request = route.request(); const url = new URL(request.url());
    if (url.pathname === '/api/projects/p1') return route.fulfill({ json: project });
    if (url.pathname === '/api/test-suites/s1') return route.fulfill({ json: suite });
    if (url.pathname.includes('environments')) {
      if (request.method() === 'POST') { environments = [{ ...request.postDataJSON(), id: 'e1', projectId: 'p1', version: 'e-v1' }]; return route.fulfill({ status: 201, json: environments[0] }); }
      return route.fulfill({ json: environments });
    }
    if (request.method() === 'POST') { cases = [{ ...request.postDataJSON(), id: 'c1', testSuiteId: 's1', catalogVersion: 1, version: 'c-v1' }]; return route.fulfill({ status: 201, json: cases[0] }); }
    if (request.method() === 'PUT') { expect(request.postDataJSON().version).toBe('c-v1'); cases = [{ ...cases[0], ...request.postDataJSON(), version: 'c-v2', catalogVersion: 2 }]; return route.fulfill({ json: cases[0] }); }
    const filtered = cases.filter(x => url.searchParams.get('status') !== 'inactive' || x.status === 'Inactive');
    return route.fulfill({ json: { items: filtered, total: filtered.length, page: 1, pageSize: 12 } });
  });
  await page.goto('/test-suites/s1');
  await page.getByRole('link', { name: 'Ver casos de teste' }).click();
  await page.getByRole('button', { name: '+ Novo caso' }).click();
  await page.getByLabel('Chave permanente').fill('LOGIN');
  await page.getByLabel('Nome do caso').fill('Login válido');
  await page.getByLabel('Tags do caso').fill('@SMOKE');
  await page.getByRole('button', { name: 'Salvar caso' }).click();
  await page.getByRole('button', { name: 'Editar Login válido' }).click();
  await expect(page.getByLabel('Chave permanente')).toBeDisabled();
  await expect(page.getByLabel('Chave permanente')).toHaveValue('login');
  await page.getByLabel('Status do caso').selectOption('Inactive');
  await page.getByRole('button', { name: 'Salvar caso' }).click();
  await page.getByLabel('Status', { exact: true }).selectOption('inactive');
  await expect(page.getByText('Inativo', { exact: true })).toBeVisible();
  await page.goto('/projects/p1');
  await page.getByRole('link', { name: 'Ambientes', exact: true }).click();
  await page.getByRole('button', { name: '+ Novo ambiente' }).click();
  await page.getByLabel('Ambiente', { exact: true }).selectOption('Production');
  await expect(page.getByLabel('Disponibilidade')).toBeDisabled();
  await page.getByLabel('Ambiente', { exact: true }).selectOption('Staging');
  await page.getByLabel('URL base').fill('https://staging.example.com');
  await page.getByLabel('Disponibilidade').selectOption('true');
  await page.getByRole('button', { name: 'Salvar ambiente' }).click();
  await expect(page.getByRole('heading', { name: 'Staging', exact: true })).toBeVisible();
  await expect(page.getByText('Habilitado', { exact: true })).toBeVisible();
});
