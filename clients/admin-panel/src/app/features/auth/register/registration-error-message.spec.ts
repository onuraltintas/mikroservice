import { getRegistrationErrorMessage } from './registration-error-message';

describe('getRegistrationErrorMessage', () => {
  it.each([
    [{ status: 409, error: { code: 'Identity.UserExists', message: 'Duplicate user' } }, 'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Giriş yapın veya şifrenizi sıfırlayın.'],
    [{ status: 400, error: { code: 'Identity.RegistrationDisabled' } }, 'Yeni kullanıcı kayıtları şu an kapalı. Lütfen daha sonra tekrar deneyin.'],
    [{ status: 400, error: { code: 'Institution.InvalidLocation' } }, 'Seçtiğiniz il ve ilçe eşleşmiyor. Lütfen konum bilgilerini kontrol edin.'],
    [{ status: 400, error: { errors: { Email: ['A valid email address is required'] } } }, 'Lütfen e-posta alanını kontrol edin.'],
    [{ status: 400, error: { errors: { Password: ['Password must contain at least one digit'] } } }, 'Lütfen şifre alanını kontrol edin.'],
    [{ status: 0, error: new ProgressEvent('error') }, 'Bağlantı kurulamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.'],
    [{ status: 429, error: {} }, 'Çok fazla deneme yapıldı. Lütfen biraz bekleyip yeniden deneyin.'],
    [{ status: 500, error: { message: 'SQL connection failed' } }, 'Kayıt şu anda tamamlanamıyor. Lütfen daha sonra yeniden deneyin.'],
    [{ status: 409, error: { message: 'database constraint violation' } }, 'Kayıt bilgileri mevcut bir kayıtla çakışıyor. Bilgileri kontrol edin.'],
    [{ status: 400, error: { message: 'unrecognized server error' } }, 'Kayıt bilgilerini kontrol edip yeniden deneyin.']
  ])('returns a friendly message for %o', (error, expected) => {
    expect(getRegistrationErrorMessage(error)).toBe(expected);
  });
});
