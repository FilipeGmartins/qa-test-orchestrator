import { expect, test } from './fixtures';

test('cria uma suíte, edita status e consulta inativas', async ({ page }) => {
  const project = { id: 'p1', name: 'Portal', description: '', archivedAt: null, version: 'p-v1', createdAt: '2026-09-14T12:00:00Z', updatedAt: '2026-09-14T12:00:00Z' };
  let suite = { id: 's1', projectId: 'p1', name: '', description: '', tags: [] as string[], status: 'Active', version: 's-v1', createdAt: project.createdAt, updatedAt: project.updatedAt };
  await page.route(/\/api\/(?:projects|test-suites)(?:[/?]|$)/, route => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.pathname === '/api/projects/p1') return route.fulfill({ json: project });
    if (request.method() === 'POST') { suite = { ...suite, ...request.postDataJSON() }; return route.fulfill({ status: 201, json: suite }); }
    if (request.method() === 'PUT') { expect(request.postDataJSON().version).toBe('s-v1'); suite = { ...suite, ...request.postDataJSON(), version: 's-v2' }; return route.fulfill({ json: suite }); }
    if (url.pathname === '/api/test-suites/s1') return route.fulfill({ json: suite });
    return route.fulfill({ json: { items: [suite], total: 1, page: 1, pageSize: 12 } });
  });
  await page.goto('/projects/p1/test-suites/new');
  await page.getByLabel('Nome da suíte').fill('Smoke checkout');
  await page.getByLabel('Tags', { exact: true }).fill('@SMOKE @checkout');
  await page.getByRole('button', { name: 'Criar suíte' }).click();
  await page.getByRole('link', { name: 'Abrir suíte Smoke checkout' }).click();
  await expect(page.getByLabel('Tags', { exact: true })).toHaveValue('@smoke @checkout');
  await page.getByLabel('Status da suíte').selectOption('Inactive');
  await page.getByRole('button', { name: 'Salvar suíte' }).click();
  await page.getByLabel('Status', { exact: true }).selectOption('inactive');
  await expect(page.getByText('Inativa', { exact: true })).toBeVisible();
});
