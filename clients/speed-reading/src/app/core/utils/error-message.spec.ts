import { getErrorMessage } from './error-message';

describe('getErrorMessage', () => {
  it('explains that a duplicate account is already registered', () => {
    expect(getErrorMessage({ error: { code: 'Identity.UserExists' } }, 'fallback'))
      .toBe('Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.');
  });

  it('reads the identity service description field', () => {
    expect(getErrorMessage({ error: { code: 'Registration.Failed', description: 'Kayıt tamamlanamadı.' } }, 'fallback'))
      .toBe('Kayıt tamamlanamadı.');
  });

  it('reads the gateway message field', () => {
    expect(getErrorMessage({ message: 'Geçersiz ilçe.' }, 'fallback'))
      .toBe('Geçersiz ilçe.');
  });
});
