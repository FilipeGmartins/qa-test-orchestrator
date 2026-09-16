import { test } from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.dirname(fileURLToPath(import.meta.url));
async function execute(config) {
  const directory = await fs.mkdtemp(path.join(root, '../.cache/runner-test-'));
  const child = spawn(process.execPath, [path.join(root, 'run.mjs')], { env: { ...process.env, QA_RUN_DIRECTORY: directory }, stdio: ['pipe', 'pipe', 'pipe'] });
  let output = ''; let error = '';
  child.stdout.on('data', x => output += x); child.stderr.on('data', x => error += x);
  child.stdin.end(JSON.stringify(config));
  const code = await new Promise((resolve, reject) => { child.on('error', reject); child.on('close', resolve); });
  const events = output.split('\n').filter(x => x.startsWith('{')).map(x => JSON.parse(x));
  return { code, events, error, directory };
}
test('real Chromium: passing cases, retries, blocked origins and timeout', { timeout: 90000 }, async () => {
  const server = http.createServer((req, res) => {
    if (req.url === '/slow') return setTimeout(() => { res.end('<title>Slow</title>'); }, 10000);
    res.writeHead(req.url === '/fail' ? 500 : 200, { 'Content-Type': 'text/html' });
    res.end('<html><title>Runner fixture</title><body>Controlled test target</body></html>');
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const config = { baseUrl: origin, allowedOrigins: [origin], cases: [{ stableKey: 'page-title' }, { stableKey: 'http-ok' }],
    options: { testType: 'Smoke', browser: 'Chromium', mode: 'Headless', workers: 2, retries: 0, timeoutSeconds: 20, screenshot: 'Never', video: 'Never', trace: 'Never' } };
  try {
    const passed = await execute({ ...config, options: { ...config.options, screenshot: 'Always', video: 'Always', trace: 'Always' } });
    assert.equal(passed.code, 0, passed.error); assert.equal(passed.events.at(-1).passed, 2);
    assert.equal(passed.events.filter(x => x.kind === 'attempt').length, 2);
    const artifacts = await fs.readdir(path.join(passed.directory, 'artifacts'), { recursive: true });
    for (const suffix of ['.png', '.webm', '.zip']) assert.ok(artifacts.some(x => x.endsWith(suffix)), `Missing ${suffix} artifact`);
    const failed = await execute({ ...config, baseUrl: origin + '/fail', cases: [{ stableKey: 'http-ok' }], options: { ...config.options, retries: 1 } });
    assert.equal(failed.code, 1); assert.equal(failed.events.at(-1).failed, 1);
    assert.equal(failed.events.filter(x => x.kind === 'attempt').length, 2);
    const denied = await execute({ ...config, allowedOrigins: [] }); assert.notEqual(denied.code, 0); assert.equal(denied.events.length, 0);
    const unknown = await execute({ ...config, cases: [{ stableKey: '../bad' }] }); assert.notEqual(unknown.code, 0);
    const timeout = await execute({ ...config, baseUrl: origin + '/slow', cases: [{ stableKey: 'page-title' }], options: { ...config.options, timeoutSeconds: 5 } });
    assert.notEqual(timeout.code, 0); assert.notEqual(timeout.events.at(-1)?.status, 'passed');
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});
