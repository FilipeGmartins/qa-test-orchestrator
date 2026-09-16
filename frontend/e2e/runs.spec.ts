import { expect, test } from '@playwright/test';

test('configura em cinco etapas, consulta snapshot, cancela e filtra histórico', async ({ page }) => {
  const project = { id: 'p1', name: 'Portal', archivedAt: null };
  const suite = { id: 's1', projectId: 'p1', name: 'Smoke', status: 'Active' };
  const item = { id: 'c1', testSuiteId: 's1', name: 'Login', stableKey: 'login', tags: ['@smoke'], catalogVersion: 1, status: 'Active' };
  const env = { id: 'e1', name: 'Staging', baseUrl: 'https://example.com/', enabled: true };
  let run: Record<string, unknown> | null = null;
  await page.route(/\/api\/(?:projects|test-suites|test-runs|runner)(?:[/?]|$)/, route => {
    const req = route.request(); const url = new URL(req.url());
    if (url.pathname === '/api/runner') return route.fulfill({ json: { enabled: false, catalog: [] } });
    if (url.pathname.endsWith('/results')) return route.fulfill({ json: { items: [], total: 0 } });
    if (url.pathname === '/api/projects/p1') return route.fulfill({ json: project });
    if (url.pathname === '/api/projects/p1/test-suites') return route.fulfill({ json: { items: [suite], total: 1 } });
    if (url.pathname === '/api/test-suites/s1/test-cases') return route.fulfill({ json: { items: [item], total: 1 } });
    if (url.pathname.endsWith('/environments')) return route.fulfill({ json: [env] });
    if (req.method() === 'POST' && url.pathname.endsWith('/cancel')) { expect(req.postDataJSON().version).toBe('v1'); run = { ...run, status: 'Cancelled', version: 'v2', finishedAt: '2026-09-15T13:00:00Z' }; return route.fulfill({ json: run }); }
    if (req.method() === 'POST') {
      const input = req.postDataJSON(); expect(input.caseIds).toEqual(['c1']); expect(input.options.workers).toBe(2); expect(input.environmentId).toBe('e1');
      run = { id: 'r1', projectId: 'p1', status: 'Pending', version: 'v1', createdAt: '2026-09-15T12:00:00Z', startedAt: null, finishedAt: null, runnerAvailable: false,
        configuration: { projectName: 'Portal', suiteName: 'Smoke', environmentName: 'Staging', baseUrl: env.baseUrl, cases: [item], tags: input.tags, options: input.options } };
      return route.fulfill({ status: 201, json: run });
    }
    if (url.pathname === '/api/test-runs/r1') return route.fulfill({ json: run });
    const items = run && (url.searchParams.get('status') === 'all' || url.searchParams.get('status') === run.status) ? [run] : [];
    return route.fulfill({ json: { items, total: items.length } });
  });
  await page.goto('/projects/p1/test-runs/new');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('alert')).toHaveText('Selecione uma suíte ativa.');
  await page.getByLabel('Smoke', { exact: true }).check();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Login · login').check();
  await page.getByLabel('Tags (opcional)').fill('@smoke');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Staging · https://example.com/').check();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Workers', { exact: true }).fill('11');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('alert')).toHaveText('Workers deve estar entre 1 e 10.');
  await page.getByLabel('Workers', { exact: true }).fill('2');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revise antes de salvar' })).toBeVisible();
  await page.getByRole('button', { name: 'Salvar como pendente' }).click();
  await expect(page.getByRole('status')).toHaveText('Pendente');
  await expect(page.getByText(/Nenhum teste é executado/)).toBeVisible();
  await page.getByRole('button', { name: 'Cancelar execução' }).click();
  await page.getByRole('button', { name: 'Confirmar cancelamento' }).click();
  await expect(page.getByRole('status')).toHaveText('Cancelada');
  await page.getByRole('link', { name: '← Voltar ao histórico' }).click();
  await page.getByLabel('Status da execução').selectOption('Cancelled');
  await expect(page.getByRole('article').getByText('Cancelada', { exact: true })).toBeVisible();
});
