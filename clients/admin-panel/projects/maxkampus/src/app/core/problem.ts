import { HttpErrorResponse } from '@angular/common/http';

/** The message a service gave (problem details or Identity's error), or the fallback in the page's own words. */
export function problemText(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Bağlantı kurulamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.';
    if (error.status === 429) return 'Çok sık denediniz. Birkaç dakika sonra yeniden deneyin.';
    const body = error.error as { detail?: unknown; message?: unknown; description?: unknown; title?: unknown } | null;
    for (const value of [body?.detail, body?.message, body?.description]) {
      if (typeof value === 'string' && value.trim()) return value;
    }
  }
  if (error instanceof Error && error.message && !(error instanceof HttpErrorResponse)) return error.message;
  return fallback;
}

/** Identity's error code ({ code } or { error: { code } }), when there is one. */
export function problemCode(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) return null;
  const body = error.error as { code?: unknown; error?: { code?: unknown } } | null;
  const code = body?.code ?? body?.error?.code;
  return typeof code === 'string' ? code : null;
}

/** Field errors of a validation problem, by field name in lower camel case. */
export function fieldErrors(error: unknown): Record<string, string> {
  const errors = error instanceof HttpErrorResponse ? (error.error as { errors?: Record<string, unknown> } | null)?.errors : null;
  const result: Record<string, string> = {};
  if (errors && typeof errors === 'object') {
    for (const [key, value] of Object.entries(errors)) {
      const text = Array.isArray(value) ? String(value[0] ?? '') : String(value ?? '');
      if (text) result[key.charAt(0).toLowerCase() + key.slice(1)] = text;
    }
  }
  return result;
}
