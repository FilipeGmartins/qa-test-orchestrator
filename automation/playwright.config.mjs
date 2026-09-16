import fs from 'node:fs';
import path from 'node:path';
const input = JSON.parse(fs.readFileSync(process.env.QA_RUN_INPUT, 'utf8'));
const o = input.options;
const browsers = { Chromium: 'chromium', Firefox: 'firefox', WebKit: 'webkit' };
const names = o.browser === 'All' ? Object.keys(browsers) : [o.browser];
if (names.some(x => !browsers[x]) || !['Headless', 'Headed'].includes(o.mode)) throw new Error('Invalid browser/mode');
const policy = (value, kind) => {
  if (!['Always', 'OnFailure', 'Never'].includes(value)) throw new Error('Invalid capture policy');
  return value === 'Never' ? 'off' : value === 'Always' ? 'on' : kind === 'screenshot' ? 'only-on-failure' : 'retain-on-failure';
};
export default {
  testDir: '.', testMatch: 'catalog.spec.mjs', fullyParallel: true,
  workers: o.workers, retries: o.retries, timeout: o.timeoutSeconds * 1000, globalTimeout: o.timeoutSeconds * 1000,
  outputDir: path.join(process.env.QA_RUN_DIRECTORY, 'artifacts'),
  reporter: [['./reporter.mjs']],
  projects: names.map(name => ({ name, use: { browserName: browsers[name] } })),
  use: { baseURL: input.baseUrl, headless: o.mode === 'Headless', serviceWorkers: 'block',
    screenshot: policy(o.screenshot, 'screenshot'), video: policy(o.video, 'video'), trace: policy(o.trace, 'trace') }
};
