import { HttpErrorResponse } from '@angular/common/http';
import { getApiErrorMessage } from './api-error-message';
import { SEN_BY_CODE, SEN_VALIDATION_FALLBACK, toSenServerMessage } from './sen-server-messages';

// Paket F L8 (user decision 07.11.2026): identity's "siz" texts are shown with "sen" on onuraltintas.net; the
// management panel (voice "siz") keeps the server text.
const bad = (body: unknown, status = 400) => new HttpErrorResponse({ status, error: body });
const formal = /(?:ınız|iniz|unuz|ünüz|yın|yin|girin|edin|seçin|Lütfen)(?=$|[\s.,!?…:;)])/i;

describe('sen server messages', () => {
  const serverTexts: Record<string, string> = {
    'Auth.ProductAccessDenied': 'Bu platform için hesabınıza erişim yetkisi tanımlı değil.',
    'Auth.MfaLocked': 'Çok fazla hatalı deneme yapıldı. Lütfen daha sonra tekrar deneyin.',
    'Auth.InvalidMfaCode': 'Doğrulama kodu geçersiz veya kullanılmış. Yeni kodla tekrar deneyin.',
    'Auth.ExistingAccountMfaRequired': 'Mevcut hesabınızın doğrulama uygulamasındaki altı haneli kodu girin.',
    'Auth.EmailNotConfirmed': 'Lütfen e-posta adresinizi doğrulayın.',
    'Auth.UserInactive': 'User account is inactive.',
    'System.MaintenanceMode': 'Sistem şu anda bakım modundadır. Lütfen daha sonra tekrar deneyiniz.',
    'Identity.RegistrationDisabled': 'Yeni kullanıcı kayıtları kapalıdır. Mevcut bir hesabınız yoksa giriş yapamazsınız.',
    'Registration.Failed': 'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.',
    'Auth.GoogleRegistrationExpired': 'Kayıt işleminizin süresi doldu. Lütfen Google ile tekrar devam edin.',
    'ResetPassword.InvalidRequest': 'Sıfırlama bağlantısı geçersiz veya süresi dolmuş olabilir. Lütfen yeni bir bağlantı isteyin.',
    'Profiles.OwnedByCoaching': 'Eğitim profilini Koçluk profil ekranından güncelleyin.',
  };

  it('covers every mapped code in this spec', () => {
    expect(Object.keys(serverTexts).sort()).toEqual(Object.keys(SEN_BY_CODE).sort());
  });

  for (const [code, text] of Object.entries(serverTexts)) {
    it(`${code}: sen on the Coaching site, the server text on the management panel`, () => {
      const error = bad({ code, description: text });
      const sen = getApiErrorMessage(error, 'yedek', { voice: 'sen' });
      expect(sen).toBe(SEN_BY_CODE[code]);
      expect(sen).not.toMatch(formal);
      expect(getApiErrorMessage(error, 'yedek', { voice: 'siz' })).toBe(text);
    });
  }

  it('rewrites the FluentValidation texts that have no code, and a still formal one becomes a generic sen text', () => {
    const error = bad({ title: 'One or more validation errors occurred.', errors: {
      Email: ['Geçerli bir e-posta adresi giriniz.'], PhoneNumber: ['Geçerli bir telefon numarası giriniz.'],
      ProvinceId: ['Geçerli bir il seçiniz.'], DistrictId: ['Geçerli bir ilçe seçiniz.'], Other: ['Bilgilerinizi kontrol ediniz.']
    } });
    expect(getApiErrorMessage(error, 'yedek', { voice: 'sen' })).toBe(
      `Geçerli bir e-posta adresi gir. Geçerli bir telefon numarası gir. Geçerli bir il seç. Geçerli bir ilçe seç. ${SEN_VALIDATION_FALLBACK}`);
    expect(getApiErrorMessage(error, 'yedek', { voice: 'siz' })).toContain('Geçerli bir e-posta adresi giriniz.');
  });

  it('rewrites texts with a variable part and texts reached without an HTTP status (login)', () => {
    expect(toSenServerMessage('Çok fazla başarısız giriş denemesi. Lütfen 7 dakika sonra tekrar deneyin.'))
      .toBe('Çok fazla başarısız giriş denemesi yapıldı. 7 dakika sonra yeniden dene.');
    expect(getApiErrorMessage({ error: { code: 'Auth.MfaLocked', message: 'Lütfen daha sonra tekrar deneyin.' } }, 'yedek', { voice: 'sen' }))
      .toBe(SEN_BY_CODE['Auth.MfaLocked']);
  });

  it('passes unknown messages through unchanged, also the Coaching service ones', () => {
    for (const text of ['Bu hedef artık düzenlenemez.', 'Planın kaydedildi ama bildirim gönderilemedi; yeniden dene.', 'Kurum adı zorunludur.'])
      expect(getApiErrorMessage(bad({ code: 'Coaching.Something', description: text }), 'yedek', { voice: 'sen' })).toBe(text);
    expect(toSenServerMessage('Bilinmeyen bir şey oldu.', null, false)).toBe('Bilinmeyen bir şey oldu.');
  });
});
