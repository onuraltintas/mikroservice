import { getAdminErrorMessage } from './admin-error-message';

describe('getAdminErrorMessage', () => {
  it('explains that MFA is required for a protected action', () => {
    expect(getAdminErrorMessage({ status: 403 }, 'İşlem başarısız.', true))
      .toContain('MFA doğrulaması gerekiyor');
  });

  it('keeps permission failures distinct from MFA failures', () => {
    expect(getAdminErrorMessage({ status: 403 }, 'Bu işlem için yetkiniz yok.'))
      .toBe('Bu işlem için yetkiniz yok.');
  });

  it('returns nested API problem details when available', () => {
    expect(getAdminErrorMessage({ status: 400, error: { error: { description: 'Geçersiz rol.' } } }, 'İşlem başarısız.'))
      .toBe('Geçersiz rol.');
  });

  it('uses the fallback when the response has no message', () => {
    expect(getAdminErrorMessage({ status: 500, error: {} }, 'İşlem başarısız.'))
      .toBe('İşlem başarısız.');
  });

  it('shows the Turkish validation messages instead of the English ProblemDetails detail', () => {
    const error = {
      status: 400,
      error: {
        title: 'Validation Error',
        detail: 'One or more validation errors occurred.',
        errors: { NewPassword: ['Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.'] },
      },
    };

    expect(getAdminErrorMessage(error, 'Şifre güncellenemedi.', true))
      .toBe('Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.');
  });

  it('reports a missing permission instead of MFA when the server says so', () => {
    const error = { status: 403, error: { code: 'Auth.Forbidden', detail: 'Bu işlem için yetkiniz yok.' } };

    expect(getAdminErrorMessage(error, 'İşlem başarısız.', true)).toBe('Bu işlem için yetkiniz yok.');
  });

  it('reports MFA when the server says MFA is missing, even without the MFA hint', () => {
    const error = { status: 403, error: { code: 'Auth.MfaRequired', detail: 'Bu işlem için MFA doğrulaması gerekiyor.' } };

    expect(getAdminErrorMessage(error, 'İşlem başarısız.')).toContain('MFA doğrulaması gerekiyor');
  });

  it('explains network failures and rate limits', () => {
    expect(getAdminErrorMessage({ status: 0, error: null }, 'İşlem başarısız.')).toContain('Sunucuya ulaşılamadı');
    expect(getAdminErrorMessage({ status: 429, error: null }, 'İşlem başarısız.')).toContain('Çok fazla deneme');
  });
});
