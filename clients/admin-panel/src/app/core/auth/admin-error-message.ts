/** Converts API errors into actionable messages for protected admin actions. */
export function getAdminErrorMessage(error: unknown, fallback: string, mfaRequired = false): string {
  if (!error || typeof error !== 'object') return fallback;

  const response = error as { status?: unknown; error?: unknown };
  if (mfaRequired && response.status === 403) {
    return 'Bu işlem için MFA doğrulaması gerekiyor. Profil ayarlarından MFA\'yı tamamlayın.';
  }

  const queue: unknown[] = [response.error];
  while (queue.length > 0) {
    const value = queue.shift();
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (!value || typeof value !== 'object') continue;

    const record = value as Record<string, unknown>;
    for (const key of ['detail', 'message', 'description', 'title']) {
      const message = record[key];
      if (typeof message === 'string' && message.trim()) return message.trim();
    }
    for (const key of ['error', 'Error']) {
      if (record[key]) queue.push(record[key]);
    }
  }

  return fallback;
}
