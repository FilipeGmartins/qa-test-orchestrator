import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
const root = path.dirname(fileURLToPath(import.meta.url));
let input = '';
for await (const chunk of process.stdin) { input += chunk; if (input.length > 200_000) throw new Error('Input too large'); }
const config = JSON.parse(input);
const catalog = JSON.parse(await fs.readFile(path.join(root, 'catalog.json'), 'utf8'));
const options = config.options;
if (!Array.isArray(config.cases) || !config.cases.length || config.cases.length > 100
    || config.cases.some(x => !catalog.some(t => t.key === x.stableKey && t.types.includes(options.testType)))) throw new Error('Unknown catalog selection');
if (!Number.isInteger(options.workers) || options.workers < 1 || options.workers > 10
    || !Number.isInteger(options.retries) || options.retries < 0 || options.retries > 5
    || !Number.isInteger(options.timeoutSeconds) || options.timeoutSeconds < 5 || options.timeoutSeconds > 300) throw new Error('Invalid limits');
const url = new URL(config.baseUrl);
if (!config.allowedOrigins.includes(url.origin) || !['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error('Origin denied');
const output = process.env.QA_RUN_DIRECTORY;
if (!output || !path.isAbsolute(output)) throw new Error('Missing server output directory');
await fs.mkdir(output, { recursive: true });
await fs.writeFile(path.join(output, 'input.json'), JSON.stringify(config));
const child = spawn(process.execPath, [path.join(root, '../frontend/node_modules/playwright/cli.js'), 'test', '--config', path.join(root, 'playwright.config.mjs')], {
  stdio: ['ignore', 'inherit', 'inherit'], shell: false,
  env: { ...process.env, QA_RUN_INPUT: path.join(output, 'input.json'), QA_RUN_DIRECTORY: output }
});
child.on('error', () => { process.exitCode = 2; });
child.on('exit', code => { process.exitCode = code ?? 2; });
