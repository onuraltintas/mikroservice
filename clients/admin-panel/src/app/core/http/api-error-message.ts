/**
 * Turns every error shape the gateway and the services return into one clear Turkish message:
 * FluentValidation ProblemDetails (`errors`), Result errors (`{ code, description }`, nested in
 * `error`/`Error`), plain strings, 403 problems (`Auth.Forbidden` / `Auth.MfaRequired`),
 * conflicts, rate limits, network failures and server errors. Generic English framework texts
 * ("One or more validation errors occurred.", "Http failure response …") are never shown.
 */
export type MessageVoice = 'sen' | 'siz';

export interface ApiErrorMessageOptions {
  /** Students and parents are addressed with "sen", staff and admins with "siz" (default). */
  voice?: MessageVoice;
  /** Shown for a 409 whose body has no message. */
  conflict?: string;
  /** Shown for a 403 whose body has no message. */
  forbidden?: string;
}

export const MFA_REQUIRED_CODE = 'Auth.MfaRequired';

const englishGenerics = [
  /^one or more validation errors occurred\.?$/i,
  /^an unexpected error occurred\.?$/i,
  /^validation error$/i,
  /^(bad request|forbidden|unauthorized|not found|conflict|too many requests|internal server error|service unavailable)$/i,
  /^http failure/i,
  /^the .+ field is required\.?$/i,
  /^'.+' (must|is not|should)/i,
];

const messages = {
  network: {
    siz: 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.',
    sen: 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.',
  },
  rateLimit: {
    siz: 'Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.',
    sen: 'Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.',
  },
};

function usable(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  const text = value.trim();
  return text.length > 0 && !englishGenerics.some(pattern => pattern.test(text));
}

function validationMessages(errors: unknown): string[] {
  const collected: unknown[] = [];
  if (Array.isArray(errors)) {
    collected.push(...errors);
  } else if (errors && typeof errors === 'object') {
    for (const value of Object.values(errors as Record<string, unknown>)) {
      if (Array.isArray(value)) collected.push(...value);
      else collected.push(value);
    }
  }

  const texts = collected
    .map(item => typeof item === 'string'
      ? item
      : (item as Record<string, unknown> | null)?.['errorMessage'] ?? (item as Record<string, unknown> | null)?.['message'])
    .filter(usable)
    .map(text => text.trim());
  return [...new Set(texts)];
}

/** Reads the most specific Turkish message in an API error body, or null. */
export function extractApiErrorMessage(body: unknown): string | null {
  const queue: unknown[] = [body];
  while (queue.length > 0) {
    const value = queue.shift();
    if (usable(value)) return value.trim();
    if (!value || typeof value !== 'object') continue;

    const record = value as Record<string, unknown>;
    const validation = validationMessages(record['errors'] ?? record['Errors']);
    if (validation.length > 0) return validation.join(' ');

    for (const key of ['detail', 'Detail', 'description', 'Description', 'message', 'Message']) {
      if (usable(record[key])) return (record[key] as string).trim();
    }
    for (const key of ['error', 'Error']) {
      if (record[key]) queue.push(record[key]);
    }
  }
  return null;
}

/** The problem code of an API error body (`code`, nested `error.code`, …), or null. */
export function getApiErrorCode(error: unknown): string | null {
  const queue: unknown[] = [(error as { error?: unknown } | null)?.error];
  while (queue.length > 0) {
    const value = queue.shift();
    if (!value || typeof value !== 'object') continue;
    const record = value as Record<string, unknown>;
    const code = record['code'] ?? record['Code'];
    if (typeof code === 'string' && code) return code;
    for (const key of ['error', 'Error']) {
      if (record[key]) queue.push(record[key]);
    }
  }
  return null;
}

export function getApiErrorMessage(error: unknown, fallback: string, options: ApiErrorMessageOptions = {}): string {
  if (!error || typeof error !== 'object') return fallback;
  const voice = options.voice ?? 'siz';
  const response = error as { status?: unknown; error?: unknown };
  if (typeof response.status !== 'number') return extractApiErrorMessage(response.error) ?? fallback;

  const status = response.status;
  if (status === 0) return messages.network[voice];
  if (status === 429) return messages.rateLimit[voice];

  const message = extractApiErrorMessage(response.error);
  if (status >= 500) return fallback;
  if (message) return message;
  if (status === 409) return options.conflict ?? fallback;
  if (status === 403) return options.forbidden ?? 'Bu işlem için yetkiniz yok.';
  return fallback;
}
