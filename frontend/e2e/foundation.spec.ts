import { expect, test } from '@playwright/test';

test('consulta diagnóstico e navega para status do sistema', async ({ page }) => {
  await page.route('**/api/system', route => route.fulfill({ json: {
    application: 'QA Test Orchestrator', version: '0.1.0', api: 'available', database: 'unavailable',
  } }));
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Uma base para testar melhor.');
  await expect(page.getByText('Indisponível', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Status do sistema' }).click();
  await expect(page).toHaveURL(/\/settings$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Status do sistema');
});

test('layout móvel não tem rolagem horizontal', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route('**/api/system', route => route.fulfill({ json: {
    application: 'QA Test Orchestrator', version: '0.1.0', api: 'available', database: 'available',
  } }));
  await page.goto('/');
  await expect(page.getByText('Conexão com o banco confirmada')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
