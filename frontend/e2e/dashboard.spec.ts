import { expect, test } from '@playwright/test';
test('dashboard filtra projeto/período, renderiza gráfico e permite investigar falhas em tela móvel', async ({ page }, testInfo) => {
  const requests: URL[] = [];
  await page.route(/\/api\/(?:dashboard|projects)(?:[/?]|$)/, route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/projects') return route.fulfill({ json: { items: [{ id: 'p1', name: 'Portal', archivedAt: null }], total: 1 } });
    requests.push(url);
    return route.fulfill({ json: { generatedAt: '2026-09-16T12:00:00Z', totalRuns: 2, runCounts: { Pending: 0, Queued: 0, Running: 0, Passed: 1, Failed: 1, Error: 0, Cancelled: 0 },
      passed: 1, failed: 1, skipped: 0, interrupted: 0, successRate: 50, averageTestDurationMs: 1200, flakyTests: 1, completedRunsWithoutDetails: 0,
      daily: [{ date: '2026-09-15', runs: 1, passed: 1, failed: 0, skipped: 0 }, { date: '2026-09-16', runs: 1, passed: 0, failed: 1, skipped: 0 }],
      recentRuns: [{ id: 'r1', projectName: 'Portal', suiteName: 'Smoke', status: 'Failed', createdAt: '2026-09-16T12:00:00Z' }],
      recentFailures: [{ runId: 'r1', caseId: 'c1', caseName: 'Página inicial', browser: 'Chromium', error: 'Expected title' }],
      flakyCases: [{ caseId: 'c1', caseName: 'Página inicial', browser: 'Chromium', recoveredRuns: 1 }] } });
  });
  await page.goto('/dashboard');
  // The first Vite request compiles the lazy chart module on slower local hosts.
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible({ timeout: 15000 });
  await expect(page.getByText('50%', { exact: true })).toBeVisible();
  await expect(page.locator('.recharts-surface').first()).toBeVisible();
  await page.getByLabel('Projeto', { exact: true }).selectOption('p1');
  await page.getByLabel('De (UTC)').fill('2026-09-15'); await page.getByLabel('Até (UTC)').fill('2026-09-16');
  await page.getByRole('button', { name: 'Aplicar filtros' }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get('projectId')).toBe('p1');
  expect(requests.at(-1)?.searchParams.get('from')).toBe('2026-09-15');
  await page.getByText('Consultar valores diários em texto').click();
  await expect(page.getByText('2026-09-15: 1 execuções · 1 aprovados · 0 falhos · 0 ignorados')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('dashboard-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('dashboard-mobile.png'), fullPage: true });
  await page.getByRole('link', { name: 'Portal / Smoke' }).click();
  await expect(page).toHaveURL(/\/test-runs\/r1$/);
});
