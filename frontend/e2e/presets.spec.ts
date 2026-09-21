import { expect, test } from '@playwright/test';
test('cria e revisa preset, edita, reutiliza sem executar e arquiva preservando revisões', async ({ page }) => {
  const project = { id: 'p1', name: 'Portal', archivedAt: null };
  const suite = { id: 's1', projectId: 'p1', name: 'Smoke', status: 'Active' };
  const item = { id: 'c1', testSuiteId: 's1', name: 'Title', stableKey: 'page-title', tags: [], catalogVersion: 1, status: 'Active' };
  const env = { id: 'e1', name: 'Staging', baseUrl: 'http://localhost', enabled: true };
  let preset: any; let run: any; const revisions: any[] = []; let createdRuns = 0;
  await page.route(/\/api\/(?:projects|test-suites|test-runs|presets|runner)(?:[/?]|$)/, route => {
    const req = route.request(); const url = new URL(req.url());
    if (url.pathname === '/api/runner') return route.fulfill({ json: { enabled: false, catalog: [] } });
    if (url.pathname === '/api/projects/p1') return route.fulfill({ json: project });
    if (url.pathname.endsWith('/test-suites')) return route.fulfill({ json: { items: [suite], total: 1 } });
    if (url.pathname.endsWith('/test-cases')) return route.fulfill({ json: { items: [item], total: 1 } });
    if (url.pathname.endsWith('/environments')) return route.fulfill({ json: [env] });
    if (url.pathname.endsWith('/revisions')) return route.fulfill({ json: { items: [...revisions].reverse(), total: revisions.length } });
    if (url.pathname.endsWith('/preview')) return route.fulfill({ json: { version: preset.version, fingerprint: 'current', configuration: preset.snapshot, revision: preset.revision, name: preset.name } });
    if (url.pathname.endsWith('/archive')) { preset.archived = true; return route.fulfill({ json: preset }); }
    if (url.pathname === '/api/presets/pr1/runs') {
      expect(req.postDataJSON()).toEqual({ version: preset.version, fingerprint: 'current' }); createdRuns++;
      run = { id: 'r1', projectId: 'p1', presetId: 'pr1', presetRevision: preset.revision, status: 'Pending', createdAt: '2026-09-16T12:00:00Z', configuration: preset.snapshot, runnerAvailable: false };
      return route.fulfill({ status: 201, json: run });
    }
    if (req.method() === 'POST' || req.method() === 'PUT') {
      const input = req.postDataJSON(); expect(input.configuration.caseIds).toEqual(['c1']);
      preset = { ...input, id: 'pr1', projectId: 'p1', archived: false, revision: (preset?.revision ?? 0) + 1, version: `v${revisions.length + 1}`,
        snapshot: { projectName: 'Portal', suiteName: 'Smoke', environmentName: 'Staging', baseUrl: env.baseUrl, cases: [item], tags: [], options: input.configuration.options } };
      revisions.push(structuredClone(preset)); return route.fulfill({ status: req.method() === 'POST' ? 201 : 200, json: preset });
    }
    if (url.pathname === '/api/presets/pr1') return route.fulfill({ json: preset });
    if (url.pathname === '/api/test-runs/r1') return route.fulfill({ json: run });
    return route.fulfill({ json: { items: preset ? [preset] : [], total: preset ? 1 : 0 } });
  });
  await page.goto('/projects/p1/presets/new');
  await page.getByLabel('Smoke', { exact: true }).check(); await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Title · page-title').check(); await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Staging · http://localhost').check(); await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByRole('button', { name: 'Continuar' }).click(); await page.getByLabel('Nome do preset').fill('Regression Full');
  await page.getByRole('button', { name: 'Salvar preset' }).click(); await expect(page.getByRole('heading', { name: 'Regression Full', exact: true })).toBeVisible(); expect(createdRuns).toBe(0);
  await page.getByRole('link', { name: 'Editar preset' }).click();
  await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByRole('button', { name: 'Remover Title', exact: true }).click();
  await expect(page.getByLabel('Title · page-title')).not.toBeChecked();
  await page.getByLabel('Title · page-title').check();
  for (let i = 0; i < 3; i++) await page.getByRole('button', { name: 'Continuar' }).click();
  await page.getByLabel('Nome do preset').fill('Debug Login'); await page.getByRole('button', { name: 'Salvar preset' }).click();
  await expect(page.getByText('Ativo · revisão 2')).toBeVisible(); expect(revisions).toHaveLength(2);
  await page.getByRole('button', { name: 'Revisar para usar' }).click(); expect(createdRuns).toBe(0);
  await page.getByRole('button', { name: 'Criar execução pendente' }).click(); await expect(page.getByRole('status')).toHaveText('Pendente'); expect(createdRuns).toBe(1);
  await page.getByRole('link', { name: 'Preset · revisão 2' }).click(); await page.getByRole('button', { name: 'Arquivar preset' }).click();
  await page.getByRole('button', { name: 'Confirmar arquivamento' }).click(); await expect(page.getByText('Arquivado · revisão 2')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Histórico de revisões' })).toBeVisible();
});
