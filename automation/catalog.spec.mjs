import { test, expect } from '../frontend/node_modules/@playwright/test/index.mjs';
import fs from 'node:fs';
const input = JSON.parse(fs.readFileSync(process.env.QA_RUN_INPUT, 'utf8'));
test.beforeEach(async ({ context }) => {
  await context.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (!input.allowedOrigins.includes(url.origin)) return route.abort('blockedbyclient');
    // Fetch without following redirects; every subsequent browser request is checked again.
    const response = await route.fetch({ maxRedirects: 0 });
    await route.fulfill({ response });
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
    } else throw new Error('Unknown trusted test');
  });
}
