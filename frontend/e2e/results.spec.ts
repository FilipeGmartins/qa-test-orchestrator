import { expect, test } from '@playwright/test';

test('acompanha resultados, baixa evidência e consulta histórico filtrado do caso', async ({ page }) => {
  const result = { id: 'a1', runId: 'r1', caseId: 'c1', caseName: 'Página inicial', stableKey: 'page-title', browser: 'Chromium', attempt: 0, status: 'failed', durationMs: 1200,
    error: 'Expected page title', stack: 'at [PATH]', logs: 'Request completed', recordedAt: '2026-09-16T12:00:00Z',
    artifacts: [{ id: 'e1', kind: 'screenshot', size: 4, available: true, expiresAt: '2026-09-30T12:00:00Z' }] };
  const options = { testType: 'Smoke', browser: 'Chromium', mode: 'Headless', workers: 1, retries: 0, timeoutSeconds: 60, screenshot: 'OnFailure', video: 'OnFailure', trace: 'OnFailure' };
  const item = { id: 'c1', name: 'Página inicial', stableKey: 'page-title', status: 'Active', tags: [], catalogVersion: 1 };
  let status = 'Pending';
  const run = () => ({ id: 'r1', projectId: 'p1', status, runnerAvailable: true, version: 'v1', createdAt: '2026-09-16T12:00:00Z',
    result: status === 'Failed' ? { passed: 0, failed: 1, skipped: 0, total: 1 } : null,
    configuration: { projectName: 'Portal', suiteName: 'Smoke', environmentName: 'Staging', baseUrl: 'http://localhost', cases: [item], tags: [], options } });
  await page.route(/\/api\/(?:projects|test-suites|test-runs|test-cases|runner)(?:[/?]|$)/, route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/runner') return route.fulfill({ json: { enabled: true, catalog: [{ key: 'page-title', name: 'Página inicial', types: ['Smoke'] }] } });
    if (url.pathname === '/api/projects/p1') return route.fulfill({ json: { id: 'p1', name: 'Portal', archivedAt: null } });
    if (url.pathname.endsWith('/test-suites')) return route.fulfill({ json: { items: [{ id: 's1', projectId: 'p1', name: 'Smoke', status: 'Active' }], total: 1 } });
    if (url.pathname === '/api/test-suites/s1/test-cases') return route.fulfill({ json: { items: [item], total: 1 } });
    if (url.pathname.endsWith('/environments')) return route.fulfill({ json: [{ id: 'e1', name: 'Staging', baseUrl: 'http://localhost', enabled: true }] });
    if (url.pathname.endsWith('/enqueue')) { status = 'Running'; return route.fulfill({ json: run() }); }
    if (route.request().method() === 'POST') { expect(route.request().postDataJSON().caseIds).toEqual(['c1']); return route.fulfill({ status: 201, json: run() }); }
    if (url.pathname.endsWith('/artifacts/e1')) return route.fulfill({ contentType: 'application/octet-stream', headers: { 'Content-Disposition': 'attachment; filename="screenshot.png"' }, body: Buffer.from([137, 80, 78, 71]) });
    if (url.pathname.endsWith('/results')) return route.fulfill({ json: { items: url.searchParams.get('status') === 'passed' ? [] : [result], total: url.searchParams.get('status') === 'passed' ? 0 : 1 } });
    if (status === 'Running') status = 'Failed';
    return route.fulfill({ json: run() });
  });
  await page.goto('/projects/p1/test-runs/new');
  await page.getByLabel('Smoke', { exact: true }).check();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Página inicial · page-title').check();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Staging · http://localhost').check();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByRole('button', { name: 'Salvar como pendente' }).click();
  await page.getByRole('button', { name: 'Executar agora' }).click();
  await page.getByRole('button', { name: 'Confirmar execução' }).click();
  await expect(page.getByRole('status')).toHaveText('Reprovada');
  await expect(page.getByRole('heading', { name: 'Resultados e evidências' })).toBeVisible();
  await expect(page.getByText('Expected page title')).toBeVisible();
  await page.getByText('Stack trace', { exact: true }).click();
  await expect(page.getByText('at [PATH]')).toBeVisible();
  const downloading = page.waitForEvent('download');
  await page.getByRole('link', { name: 'Baixar Screenshot' }).click();
  expect((await downloading).suggestedFilename()).toBe('screenshot.png');
  await page.getByRole('link', { name: 'Histórico deste caso' }).click();
  await expect(page.getByRole('heading', { name: 'Resultados do caso' })).toBeVisible();
  await page.getByLabel('Resultado da tentativa').selectOption('passed');
  await expect(page.getByText('0 tentativas encontradas')).toBeVisible();
});
