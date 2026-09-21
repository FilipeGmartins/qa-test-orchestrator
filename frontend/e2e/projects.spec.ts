import { expect, test } from './fixtures';
import type { Project } from '../src/features/projects/api';

test('cria, edita, arquiva e encontra o projeto no histórico de arquivados', async ({ page }) => {
  let saved: Project | undefined;
  await page.route('**/api/projects**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname === '/api/projects') {
      const body = request.postDataJSON();
      expect(body).toEqual({ name: 'Portal cliente', description: 'Jornada de compras' });
      saved = { ...body, id: '11111111-1111-1111-1111-111111111111', version: '22222222-2222-2222-2222-222222222222', createdAt: '2026-09-14T12:00:00Z', updatedAt: '2026-09-14T12:00:00Z', archivedAt: null };
      return route.fulfill({ status: 201, json: saved });
    }
    if (request.method() === 'PUT') {
      expect(request.postDataJSON().version).toBe(saved!.version);
      saved = { ...saved!, ...request.postDataJSON(), version: '33333333-3333-3333-3333-333333333333' };
      return route.fulfill({ json: saved });
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/archive')) {
      expect(request.postDataJSON().version).toBe(saved!.version);
      saved = { ...saved!, archivedAt: '2026-09-14T13:00:00Z', version: '44444444-4444-4444-4444-444444444444' };
      return route.fulfill({ json: saved });
    }
    if (url.pathname === '/api/projects') {
      const matches = saved && (url.searchParams.get('status') === 'archived' ? saved.archivedAt : !saved.archivedAt);
      return route.fulfill({ json: { items: matches ? [saved] : [], total: matches ? 1 : 0, page: 1, pageSize: 12 } });
    }
    return route.fulfill({ json: saved });
  });
  await page.goto('/projects');
  await page.getByRole('link', { name: 'Criar primeiro projeto' }).click();
  await page.getByRole('button', { name: 'Criar projeto', exact: true }).click();
  await expect(page.getByText('Informe um nome com 1 a 120 caracteres.')).toBeVisible();
  await page.getByLabel('Nome do projeto').fill('Portal cliente');
  await page.getByLabel('Descrição').fill('Jornada de compras');
  await page.getByRole('button', { name: 'Criar projeto', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Portal cliente');
  await page.getByRole('link', { name: 'Editar projeto' }).click();
  await page.getByLabel('Nome do projeto').fill('Portal atualizado');
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Portal atualizado');
  await page.getByRole('button', { name: 'Arquivar projeto', exact: true }).click();
  await page.getByRole('button', { name: 'Manter ativo' }).click();
  expect(saved!.archivedAt).toBeNull();
  await page.getByRole('button', { name: 'Arquivar projeto', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar arquivamento' }).click();
  await expect(page.getByText('Projeto arquivado. As informações foram preservadas.')).toBeVisible();
  await page.getByRole('link', { name: 'Todos os projetos' }).click();
  await expect(page.getByRole('link', { name: 'Abrir projeto Portal atualizado' })).toHaveCount(0);
  await page.getByLabel('Status', { exact: true }).selectOption('archived');
  await page.getByRole('link', { name: 'Abrir projeto Portal atualizado' }).click();
  await expect(page.getByText('Jornada de compras')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Editar projeto' })).toHaveCount(0);
});

test('formulário móvel preserva dados quando PostgreSQL está indisponível', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route('**/api/projects', route => route.fulfill({ status: 503, json: { error: 'DATABASE_UNAVAILABLE', message: 'Banco indisponível ou ainda não preparado.' } }));
  await page.goto('/projects/new');
  await page.getByLabel('Nome do projeto').fill('Portal mobile');
  await page.getByRole('button', { name: 'Criar projeto', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Banco indisponível');
  await expect(page.getByLabel('Nome do projeto')).toHaveValue('Portal mobile');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
