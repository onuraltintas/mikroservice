import { MFA_REQUIRED_CODE, extractApiErrorMessage, getApiErrorCode, getApiErrorMessage } from '../http/api-error-message';

const MFA_MESSAGE = 'Bu işlem için MFA doğrulaması gerekiyor. Profil ayarlarından MFA\'yı tamamlayın.';

/**
 * Converts API errors into actionable messages for protected admin actions.
 *
 * A 403 now carries `Auth.MfaRequired` or `Auth.Forbidden`; only the first one (or, for a service
 * that still answers an empty 403, the caller's `mfaRequired` hint) is reported as missing MFA.
 */
export function getAdminErrorMessage(error: unknown, fallback: string, mfaRequired = false): string {
  if (!error || typeof error !== 'object') return fallback;

  const response = error as { status?: unknown; error?: unknown };
  if (response.status === 403) {
    if (getApiErrorCode(error) === MFA_REQUIRED_CODE) return MFA_MESSAGE;
    const message = extractApiErrorMessage(response.error);
    if (message) return message;
    return mfaRequired ? MFA_MESSAGE : fallback;
  }

  return getApiErrorMessage(error, fallback);
}
