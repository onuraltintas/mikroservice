import { HttpErrorResponse } from '@angular/common/http';
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

  it('turns request timeouts into a clear localized message', () => {
    expect(getErrorMessage({ name: 'TimeoutError', message: 'Timeout has occurred' }, 'fallback'))
      .toBe('İstek zaman aşımına uğradı. Bağlantınızı kontrol edip tekrar deneyin.');
  });

  it('joins the Turkish validation messages of a ProblemDetails instead of its English detail', () => {
    const error = new HttpErrorResponse({ status: 400, error: {
      title: 'Validation Error', detail: 'One or more validation errors occurred.',
      errors: { Password: ['Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.'], Email: ['Geçerli bir e-posta adresi giriniz.'] },
    } });
    expect(getErrorMessage(error, 'fallback'))
      .toBe('Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir. Geçerli bir e-posta adresi giriniz.');
  });

  it('never shows the English HttpErrorResponse text or generic server detail', () => {
    expect(getErrorMessage(new HttpErrorResponse({ status: 400, error: null, url: '/api/x' }), 'fallback')).toBe('fallback');
    expect(getErrorMessage(new HttpErrorResponse({ status: 500, error: { detail: 'An unexpected error occurred.' } }), 'fallback')).toBe('fallback');
  });

  it('explains network failures and rate limits in the requested voice', () => {
    expect(getErrorMessage(new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') }), 'fallback'))
      .toBe('Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.');
    expect(getErrorMessage(new HttpErrorResponse({ status: 0 }), 'fallback', { voice: 'sen' }))
      .toBe('Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.');
    expect(getErrorMessage(new HttpErrorResponse({ status: 429 }), 'fallback'))
      .toBe('Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.');
    expect(getErrorMessage(new HttpErrorResponse({ status: 429 }), 'fallback', { voice: 'sen' }))
      .toBe('Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.');
  });

  it('uses the duplicate account text for an empty 409 on registration forms', () => {
    expect(getErrorMessage(new HttpErrorResponse({ status: 409 }), 'fallback')).toBe('fallback');
    expect(getErrorMessage(new HttpErrorResponse({ status: 409 }), 'fallback', { conflictIsDuplicateAccount: true }))
      .toBe('Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.');
    expect(getErrorMessage(new HttpErrorResponse({ status: 409 }), 'fallback', { voice: 'sen', conflictIsDuplicateAccount: true }))
      .toBe('Bu e-posta adresiyle zaten kayıtlı bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.');
  });
});
