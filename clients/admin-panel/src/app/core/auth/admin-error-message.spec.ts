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
});
