const duplicateAccountMessage =
  'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.';

/**
 * Converts the different error shapes returned by the API gateway and the
 * identity service into a message that can be shown to a user.
 */
export function getErrorMessage(error: any, fallback: string): string {
  const body = error?.error ?? error;
  const code = body?.code ?? body?.Code;

  if (code === 'Identity.UserExists') {
    return duplicateAccountMessage;
  }

  if (typeof body === 'string' && body.trim()) {
    return body;
  }

  const validationErrors = body?.errors;
  if (Array.isArray(validationErrors) && validationErrors.length > 0) {
    return String(validationErrors[0]);
  }

  return body?.Message
    || body?.message
    || body?.description
    || body?.Description
    || error?.message
    || fallback;
}
