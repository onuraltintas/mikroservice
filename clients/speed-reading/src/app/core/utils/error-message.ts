export type MessageVoice = 'sen' | 'siz';

export interface ErrorMessageOptions {
  /** Students are addressed with "sen"; teachers, schools and admins with "siz" (default). */
  voice?: MessageVoice;
  /** Registration forms: an empty 409 means the e-mail address is already registered. */
  conflictIsDuplicateAccount?: boolean;
}

const duplicateAccountMessage: Record<MessageVoice, string> = {
  siz: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.',
  sen: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.',
};
const networkMessage: Record<MessageVoice, string> = {
  siz: 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.',
  sen: 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.',
};
const rateLimitMessage: Record<MessageVoice, string> = {
  siz: 'Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.',
  sen: 'Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.',
};

/** Framework texts in English that must never reach the user. */
const englishGenerics = [
  /^one or more validation errors occurred\.?$/i,
  /^an unexpected error occurred\.?$/i,
  /^validation error$/i,
  /^(bad request|forbidden|unauthorized|not found|conflict|too many requests|internal server error|service unavailable)$/i,
  /^http failure/i,
  /^the .+ field is required\.?$/i,
  /^'.+' (must|is not|should)/i,
];

function usable(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  const text = value.trim();
  return text.length > 0 && !englishGenerics.some(pattern => pattern.test(text));
}

function validationMessages(errors: unknown): string[] {
  const items: unknown[] = Array.isArray(errors)
    ? errors
    : errors && typeof errors === 'object'
      ? Object.values(errors as Record<string, unknown>).flatMap(value => Array.isArray(value) ? value : [value])
      : [];
  const texts = items
    .map(item => typeof item === 'string' ? item : (item as any)?.errorMessage ?? (item as any)?.message)
    .filter(usable)
    .map(text => text.trim());
  return [...new Set(texts)];
}

/**
 * Converts the different error shapes returned by the API gateway and the
 * identity service into a message that can be shown to a user: FluentValidation
 * ProblemDetails (`errors`), Result errors (`{ code, description }`), plain strings,
 * conflicts, rate limits, network failures and server errors.
 */
export function getErrorMessage(error: any, fallback: string, options: ErrorMessageOptions = {}): string {
  const voice = options.voice ?? 'siz';
  if (error?.name === 'TimeoutError') {
    return voice === 'sen'
      ? 'İstek zaman aşımına uğradı. Bağlantını kontrol edip tekrar dene.'
      : 'İstek zaman aşımına uğradı. Bağlantınızı kontrol edip tekrar deneyin.';
  }

  const isHttpError = typeof error?.status === 'number';
  const status: number | undefined = isHttpError ? error.status : undefined;
  if (status === 0) return networkMessage[voice];
  if (status === 429) return rateLimitMessage[voice];

  const body = isHttpError ? error.error : (error?.error ?? error);
  const code = body?.code ?? body?.Code;
  if (code === 'Identity.UserExists') {
    return duplicateAccountMessage[voice];
  }

  if (status !== undefined && status >= 500) return fallback;

  if (usable(body)) {
    return body.trim();
  }

  const validation = validationMessages(body?.errors ?? body?.Errors);
  if (validation.length > 0) {
    return validation.join(' ');
  }

  for (const candidate of [body?.detail, body?.Detail, body?.Message, body?.message, body?.description, body?.Description,
    body?.error?.description, body?.error?.message, body?.Error?.Description, body?.Error?.Message]) {
    if (usable(candidate)) return candidate.trim();
  }

  if (status === 409 && options.conflictIsDuplicateAccount) return duplicateAccountMessage[voice];
  if (!isHttpError && usable(error?.message)) return error.message.trim();
  return fallback;
}
