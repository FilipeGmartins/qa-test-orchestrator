import fs from 'node:fs';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { sanitize } from './sanitize.mjs';
const emit = value => process.stdout.write(JSON.stringify(value) + '\n');
export default class Reporter {
  onBegin(_config, suite) { this.suite = suite; emit({ kind: 'begin', total: suite.allTests().length }); }
  onError() { this.infrastructure = true; }
  onTestEnd(test, result) {
    if (result.errors.some(e => /browserType.launch|Executable doesn.t exist|browser has been closed|Target page, context or browser has been closed/.test(e.message ?? ""))) this.infrastructure = true;
    const artifacts = [];
    const root = fs.realpathSync(process.env.QA_RUN_DIRECTORY);
    const evidence = path.join(root, 'evidence');
    fs.mkdirSync(evidence, { recursive: true });
    for (const attachment of result.attachments.slice(0, 10)) {
      const kind = ({ 'image/png': 'screenshot', 'video/webm': 'video', 'application/zip': 'trace' })[attachment.contentType];
      if (!kind || !attachment.path) continue;
      const source = fs.realpathSync(attachment.path);
      if (!source.startsWith(root + path.sep)) continue;
      const stat = fs.statSync(source);
      if (!stat.isFile() || stat.size > 200 * 1024 * 1024) continue;
      const id = randomUUID();
      const extension = { screenshot: '.png', video: '.webm', trace: '.zip' }[kind];
      const relativePath = `evidence/${id}${extension}`;
      fs.copyFileSync(source, path.join(root, relativePath), fs.constants.COPYFILE_EXCL);
      artifacts.push({ id, relativePath, kind, size: stat.size });
    }
    emit({ kind: 'attempt', key: test.title, browser: test.parent.project().name, attempt: result.retry,
      status: result.status, durationMs: result.duration,
      error: sanitize(result.errors.map(x => x.message ?? '').join('\n')),
      stack: sanitize(result.errors.map(x => x.stack ?? '').join('\n')),
      logs: sanitize([...result.stdout, ...result.stderr].map(x => String(x).slice(0, 4000)).join('\n')),
      artifacts });
  }
  onEnd(result) {
    const tests = this.suite?.allTests() ?? [];
    const passed = tests.filter(t => ['expected', 'flaky'].includes(t.outcome())).length;
    const skipped = tests.filter(t => t.outcome() === 'skipped').length;
    const failed = tests.length - passed - skipped;
    emit({ kind: 'end', status: this.infrastructure ? 'infrastructure' : result.status, total: tests.length, passed, failed, skipped });
  }
}
