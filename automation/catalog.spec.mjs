import { test, expect } from '../frontend/node_modules/@playwright/test/index.mjs';
import fs from 'node:fs';
const input = JSON.parse(fs.readFileSync(process.env.QA_RUN_INPUT, 'utf8'));
test.beforeEach(async ({ context }, testInfo) => {
  await context.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (testInfo.title.startsWith('frontend-') && !['GET', 'HEAD'].includes(route.request().method())) return route.abort('blockedbyclient');
    if (!input.allowedOrigins.includes(url.origin)) return route.abort('blockedbyclient');
    // Playwright routes only the first URL in a redirect chain. Never forward a
    // redirect to the browser, which could follow later hops without this guard.
    try {
      const response = await route.fetch({ maxRedirects: 0 });
      if (response.status() >= 300 && response.status() < 400 && response.headers().location) return route.abort('blockedbyclient');
      await route.fulfill({ response });
    } catch { await route.abort('failed').catch(() => {}); }
  });
  await context.routeWebSocket('**/*', socket => socket.close());
});
for (const item of input.cases) {
  test(item.stableKey, async ({ page, request }) => {
    if (item.stableKey === 'page-title') {
      const response = await page.goto(input.baseUrl);
      expect(response?.ok()).toBe(true);
      await expect(page).toHaveTitle(/\S+/);
    } else if (item.stableKey === 'http-ok') {
      const response = await request.get(input.baseUrl, { maxRedirects: 0 });
      expect(response.ok()).toBe(true);
    } else if (/^frontend-(load|console|layout)-(desktop|tablet|mobile)$/.test(item.stableKey)) {
      const [, check, device] = item.stableKey.split('-');
      const viewports = { desktop: { width: 1440, height: 900 }, tablet: { width: 768, height: 1024 }, mobile: { width: 390, height: 844 } };
      await page.setViewportSize(viewports[device]);
      const errors = []; const resources = [];
      const collect = (list, message) => { if (list.length < 20) list.push(String(message).slice(0, 512)); };
      page.on('pageerror', error => collect(errors, error.message));
      page.on('console', message => { if (message.type() === 'error') collect(errors, message.text()); });
      page.on('requestfailed', request => collect(resources, `${request.resourceType()}: ${request.failure()?.errorText ?? 'falha'}`));
      page.on('response', response => { if (response.status() >= 400) collect(resources, `${response.request().resourceType()}: HTTP ${response.status()}`); });
      const response = await page.goto(input.baseUrl, { waitUntil: 'domcontentloaded', timeout: 30000 });
      expect(response?.ok(), 'A URL final deve responder HTTP 2xx sem redirecionamentos').toBe(true);
      // Bounded observation window, not an assertion that all asynchronous activity has finished.
      await page.waitForTimeout(1500);
      if (check === 'load') expect(resources, 'Recursos com falha ou bloqueados pela política de rede').toEqual([]);
      if (check === 'console') expect(errors, 'Erros JavaScript ou console.error durante a observação').toEqual([]);
      if (check === 'layout') {
        const layout = await page.evaluate(() => ({ width: window.innerWidth, content: document.documentElement.scrollWidth }));
        expect(layout.content, `Conteúdo ${layout.content}px excede a tela ${layout.width}px`).toBeLessThanOrEqual(layout.width + 1);
      }
    } else throw new Error('Unknown trusted test');
  });
}
