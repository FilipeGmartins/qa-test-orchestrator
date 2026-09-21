import { expect, test } from './fixtures';
test('revisa URL, salva pendente e executa somente após confirmação', async ({ page }, testInfo) => {
  let run: any; let enqueues = 0;
  await page.route(url => /^\/api\/(projects|runner|test-runs)(\/|$)/.test(url.pathname), route => {
    const req = route.request(); const path = new URL(req.url()).pathname;
    if (path === '/api/runner') return route.fulfill({ json: { enabled: true, catalog: [] } });
    if (path === '/api/projects') return route.fulfill({ json: { items: [{ id: 'p1', name: 'Portal' }], total: 1 } });
    if (path.endsWith('/environments')) return route.fulfill({ json: [{ id: 'e1', name: 'Staging', baseUrl: 'https://staging.example', enabled: true }] });
    if (path.endsWith('/page-tests')) {
      const input = req.postDataJSON(); expect(input.url).toBe('https://staging.example/catalog'); expect(input.devices).toHaveLength(3); expect(input.checks).toHaveLength(3);
      run = { id: 'r1', projectId: 'p1', status: 'Pending', version: 'v1', runnerAvailable: true, createdAt: '2026-09-21T12:00:00Z', configuration: { projectName: 'Portal', suiteName: 'Frontend por URL', environmentName: 'Staging', baseUrl: 'https://staging.example', pageUrl: input.url, cases: [], tags: [], options: { browser: 'Chromium' } } };
      return route.fulfill({ status: 201, json: run });
    }
    if (path.endsWith('/enqueue')) { enqueues++; run = { ...run, status: 'Passed', result: { passed: 9, failed: 0, skipped: 0, total: 9 } }; return route.fulfill({ json: run }); }
    if (path.endsWith('/results')) return route.fulfill({ json: { items: [], total: 0 } });
    if (path === '/api/test-runs/r1') return route.fulfill({ json: run });
    return route.fulfill({ json: { items: [], total: 0 } });
  });
  await page.goto('/page-tests'); await page.getByLabel('Projeto', { exact: true }).selectOption('p1');
  await page.getByLabel('Ambiente', { exact: true }).selectOption('e1'); await page.getByLabel('URL da página').fill('https://staging.example/catalog');
  await page.getByRole('button', { name: 'Revisar configuração' }).click(); await expect(page.getByText(/9 verificações/)).toBeVisible();
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: testInfo.outputPath('page-tests-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: testInfo.outputPath('page-tests-mobile.png'), fullPage: true });
  expect(enqueues).toBe(0); await page.getByRole('button', { name: 'Salvar teste como pendente' }).click();
  await expect(page.getByRole('status')).toHaveText('Pendente'); expect(enqueues).toBe(0);
  await expect(page.getByRole('link', { name: 'https://staging.example/catalog' })).toBeVisible();
  await page.getByRole('button', { name: 'Executar agora' }).click(); await page.getByRole('button', { name: 'Confirmar execução' }).click();
  await expect(page.getByRole('status').first()).toHaveText('Aprovada'); expect(enqueues).toBe(1);
});
