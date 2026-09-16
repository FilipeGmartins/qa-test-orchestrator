const emit = value => process.stdout.write(JSON.stringify(value) + '\n');
export default class Reporter {
  onBegin(_config, suite) { this.suite = suite; emit({ kind: 'begin', total: suite.allTests().length }); }
  onError() { this.infrastructure = true; }
  onTestEnd(test, result) {
    if (result.errors.some(e => /browserType.launch|Executable doesn.t exist|browser has been closed|Target page, context or browser has been closed/.test(e.message ?? ""))) this.infrastructure = true;
    emit({ kind: 'attempt', key: test.title, browser: test.parent.project().name, attempt: result.retry,
      status: result.status, durationMs: result.duration });
  }
  onEnd(result) {
    const tests = this.suite?.allTests() ?? [];
    const passed = tests.filter(t => ['expected', 'flaky'].includes(t.outcome())).length;
    const skipped = tests.filter(t => t.outcome() === 'skipped').length;
    const failed = tests.length - passed - skipped;
    emit({ kind: 'end', status: this.infrastructure ? 'infrastructure' : result.status, total: tests.length, passed, failed, skipped });
  }
}
