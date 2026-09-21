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
    const registered = passed.events.filter(x => x.kind === 'attempt').flatMap(x => x.artifacts);
    for (const kind of ['screenshot', 'video', 'trace']) assert.ok(registered.some(x => x.kind === kind), `Missing ${kind} registration`);
    for (const item of registered) {
      assert.match(item.relativePath, /^evidence\/[0-9a-f-]+\.(png|webm|zip)$/);
      assert.equal((await fs.stat(path.join(passed.directory, item.relativePath))).size, item.size);
    }
    const failed = await execute({ ...config, baseUrl: origin + '/fail', cases: [{ stableKey: 'http-ok' }], options: { ...config.options, retries: 1 } });
    assert.equal(failed.code, 1); assert.equal(failed.events.at(-1).failed, 1);
    assert.equal(failed.events.filter(x => x.kind === 'attempt').length, 2);
    assert.ok(failed.events.filter(x => x.kind === 'attempt').every(x => x.error.length > 0 && x.stack.length > 0));
    assert.ok(!JSON.stringify(failed.events).includes(root));
    const denied = await execute({ ...config, allowedOrigins: [] }); assert.notEqual(denied.code, 0); assert.equal(denied.events.length, 0);
    const unknown = await execute({ ...config, cases: [{ stableKey: '../bad' }] }); assert.notEqual(unknown.code, 0);
    const timeout = await execute({ ...config, baseUrl: origin + '/slow', cases: [{ stableKey: 'page-title' }], options: { ...config.options, timeoutSeconds: 5 } });
    assert.notEqual(timeout.code, 0); assert.notEqual(timeout.events.at(-1)?.status, 'passed');
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});


test('frontend URL: screenshots in three viewports, resource/JS/layout failures and network fences', { timeout: 120000 }, async () => {
  let writes = 0; let outsideRequests = 0;
  const outside = http.createServer((_req, res) => { outsideRequests++; res.end('denied'); });
  await new Promise(resolve => outside.listen(0, '127.0.0.1', resolve));
  const outsideOrigin = `http://127.0.0.1:${outside.address().port}`;
  const server = http.createServer((req, res) => {
    if (req.method === 'POST') { writes++; res.end('write'); return; }
    if (req.url === '/missing') { res.writeHead(404); res.end('missing'); return; }
    if (req.url === '/redirect') { res.writeHead(302, { Location: outsideOrigin }); res.end(); return; }
    res.writeHead(200, { 'Content-Type': 'text/html' });
    const body = req.url === '/bad' ? '<img src="/missing"><div style="width:2200px">overflow</div><script>console.error("controlled console failure"); setTimeout(() => { throw new Error("controlled JS failure"); }, 100);</script>' : req.url === '/write' ? '<script>fetch("/write", {method:"POST"}).catch(() => {});</script>' : '<h1>Responsive page</h1>';
    res.end(`<html><head><title>Frontend</title><meta name="viewport" content="width=device-width"></head><body style="margin:0">${body}</body></html>`);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const config = { baseUrl: origin, allowedOrigins: [origin], cases: ['load','console','layout'].flatMap(check => ['desktop','tablet','mobile'].map(device => ({ stableKey: `frontend-${check}-${device}` }))),
    options: { testType: 'Smoke', browser: 'Chromium', mode: 'Headless', workers: 2, retries: 0, timeoutSeconds: 60, screenshot: 'Always', video: 'Never', trace: 'OnFailure' } };
  try {
    const passed = await execute(config); assert.equal(passed.code, 0, passed.error); assert.equal(passed.events.at(-1).passed, 9);
    for (const attempt of passed.events.filter(x => x.kind === 'attempt')) {
      const shot = attempt.artifacts.find(x => x.kind === 'screenshot'); assert.ok(shot);
      const bytes = await fs.readFile(path.join(passed.directory, shot.relativePath));
      const dimensions = { desktop: [1440,900], tablet: [768,1024], mobile: [390,844] }[attempt.key.split('-').at(-1)];
      assert.deepEqual([bytes.readUInt32BE(16), bytes.readUInt32BE(20)], dimensions);
    }
    const failed = await execute({ ...config, baseUrl: origin + '/bad', cases: ['load','console','layout'].map(check => ({ stableKey: `frontend-${check}-mobile` })) });
    assert.equal(failed.code, 1); assert.equal(failed.events.at(-1).failed, 3);
    const denied = await execute({ ...config, baseUrl: origin + '/redirect', cases: [{ stableKey: 'frontend-load-desktop' }] });
    assert.equal(denied.code, 1); assert.equal(outsideRequests, 0);
    const write = await execute({ ...config, baseUrl: origin + '/write', cases: [{ stableKey: 'frontend-load-desktop' }] });
    assert.equal(write.code, 1); assert.equal(writes, 0);
  } finally { server.closeAllConnections(); outside.closeAllConnections(); await Promise.all([new Promise(resolve => server.close(resolve)), new Promise(resolve => outside.close(resolve))]); }
});
