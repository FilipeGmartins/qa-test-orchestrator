export function sanitize(value, limit = 4000) {
  return String(value ?? '').slice(0, 16000)
    .replace(/\x1b\[[0-9;]*m/g, '')
    .replace(/https?:\/\/[^\s<>"']+/gi, '[URL]')
    .replace(/(?:[a-z]:\\|\/)(?:[^\s<>"']+[\\/])+[^\s<>"']*/gi, '[PATH]')
    .replace(/\b(?:authorization|cookie|set-cookie)\s*[:=][^\r\n]*/gi, '[REDACTED]')
    .replace(/\b(?:password|passwd|secret|token|api[_-]?key)\s*["']?\s*[:=]\s*["']?[^\s,"'}]+/gi, '[REDACTED]')
    .replace(/\bBearer\s+\S+/gi, '[REDACTED]').slice(0, limit);
}
