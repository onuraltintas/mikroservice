import { HttpErrorResponse } from '@angular/common/http';
import { getApiErrorMessage } from './api-error-message';

const fallback = 'İşlem tamamlanamadı.';
const response = (status: number, error: unknown) =>
  new HttpErrorResponse({ status, error, statusText: 'x', url: '/api/x' });

describe('getApiErrorMessage', () => {
  it('joins the Turkish FluentValidation messages instead of the English ProblemDetails detail', () => {
    const error = response(400, {
      title: 'Validation Error',
      detail: 'One or more validation errors occurred.',
      errors: { NewPassword: ['Şifre en az bir rakam içermelidir.', 'Şifre en az bir rakam içermelidir.'], Email: ['Geçerli bir e-posta adresi giriniz.'] },
    });

    expect(getApiErrorMessage(error, fallback))
      .toBe('Şifre en az bir rakam içermelidir. Geçerli bir e-posta adresi giriniz.');
  });

  it('reads validation errors sent as a list', () => {
    expect(getApiErrorMessage(response(400, { errors: [{ errorMessage: 'Ad alanı zorunludur.' }, 'Soyad alanı zorunludur.'] }), fallback))
      .toBe('Ad alanı zorunludur. Soyad alanı zorunludur.');
  });

  it('reads Result errors in every shape the Identity API returns', () => {
    expect(getApiErrorMessage(response(400, { code: 'X', description: 'Geçersiz kod.' }), fallback)).toBe('Geçersiz kod.');
    expect(getApiErrorMessage(response(400, { error: { code: 'X', description: 'İç içe hata.' } }), fallback)).toBe('İç içe hata.');
    expect(getApiErrorMessage(response(400, { Error: { Code: 'X', Description: 'Büyük harfli hata.' } }), fallback)).toBe('Büyük harfli hata.');
    expect(getApiErrorMessage(response(400, { Error: 'Düz metin hata.' }), fallback)).toBe('Düz metin hata.');
    expect(getApiErrorMessage(response(400, 'Metin gövde.'), fallback)).toBe('Metin gövde.');
  });

  it('never shows the generic English validation or server text', () => {
    expect(getApiErrorMessage(response(400, { detail: 'One or more validation errors occurred.', title: 'Validation Error' }), fallback)).toBe(fallback);
    expect(getApiErrorMessage(response(500, { detail: 'An unexpected error occurred.', title: 'Internal Server Error' }), fallback)).toBe(fallback);
    expect(getApiErrorMessage(response(400, { errors: { Password: ['The Password field is required.'] } }), fallback)).toBe(fallback);
  });

  it('explains network failures and rate limits in the requested voice', () => {
    expect(getApiErrorMessage(response(0, new ProgressEvent('error')), fallback))
      .toBe('Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.');
    expect(getApiErrorMessage(response(0, null), fallback, { voice: 'sen' }))
      .toBe('Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.');
    expect(getApiErrorMessage(response(429, { title: 'Too Many Requests' }), fallback))
      .toBe('Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.');
    expect(getApiErrorMessage(response(429, null), fallback, { voice: 'sen' }))
      .toBe('Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.');
  });

  it('keeps the server conflict text and falls back to the caller conflict text', () => {
    expect(getApiErrorMessage(response(409, { code: 'Identity.UserExists', description: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var.' }), fallback))
      .toBe('Bu e-posta adresiyle zaten kayıtlı bir hesap var.');
    expect(getApiErrorMessage(response(409, null), fallback, { conflict: 'Bu e-posta zaten kullanılıyor.' }))
      .toBe('Bu e-posta zaten kullanılıyor.');
  });

  it('separates permission and MFA failures on 403', () => {
    expect(getApiErrorMessage(response(403, { code: 'Auth.Forbidden', detail: 'Bu işlem için yetkiniz yok.' }), fallback))
      .toBe('Bu işlem için yetkiniz yok.');
    expect(getApiErrorMessage(response(403, { code: 'Auth.MfaRequired', detail: 'Bu işlem için MFA doğrulaması gerekiyor.' }), fallback))
      .toBe('Bu işlem için MFA doğrulaması gerekiyor.');
    expect(getApiErrorMessage(response(403, null), fallback)).toBe('Bu işlem için yetkiniz yok.');
  });

  it('uses the fallback for unknown values', () => {
    expect(getApiErrorMessage(undefined, fallback)).toBe(fallback);
    expect(getApiErrorMessage(new Error('boom'), fallback)).toBe(fallback);
  });
});
