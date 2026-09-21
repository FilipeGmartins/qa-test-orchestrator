import { test as base, expect } from '@playwright/test';
export { expect };
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.route('**/api/auth/me', route => route.fulfill({ json: { id: 'admin', login: 'admin', name: 'Test Admin', role: 'Admin', active: true, version: 'v1' } }));
    await page.route('**/api/auth/csrf', route => route.fulfill({ json: { token: 'test-token' } }));
    await use(page);
  },
});
