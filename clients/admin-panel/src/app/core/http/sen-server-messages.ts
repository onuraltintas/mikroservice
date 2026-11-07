/**
 * Paket F L8 (user decision 07.11.2026): on onuraltintas.net the identity messages are shown with "sen" too. The
 * identity service keeps its "siz" texts (the eduivme.com management panel and the staff portal use them), so the
 * Coaching pages rewrite the ones that reach them here: first by error code, then by the exact server text (the
 * FluentValidation messages have no stable code), then a few texts with a variable part. Anything else passes through
 * unchanged — the Coaching service already writes "sen".
 *
 * Used only when the page's voice is "sen" (see getApiErrorMessage and features/auth/auth-voice.ts).
 */

/** Error codes whose every server text is replaced. */
export const SEN_BY_CODE: Readonly<Record<string, string>> = {
  'Auth.ProductAccessDenied': 'Hesabının bu platforma erişim yetkisi yok.',
  // F4 (user decision 07.11.2026): product access without any role in it; only an admin restores it (no join hint).
  'Auth.NoRoleForProduct': 'Bu platformda hesabına tanımlı bir yetki kalmadı. Yeniden erişim için kurumunla ya da destekle iletişime geç.',
  'Auth.MfaLocked': 'Çok fazla hatalı doğrulama denemesi yapıldı. Biraz sonra yeniden dene.',
  'Auth.InvalidMfaCode': 'Doğrulama kodu geçersiz ya da kullanılmış. Uygulamadaki yeni kodu yaz.',
  'Auth.ExistingAccountMfaRequired': 'Mevcut hesabının doğrulama uygulamasındaki 6 haneli kodu gir.',
  'Auth.EmailNotConfirmed': 'Giriş yapmadan önce e-posta adresini doğrula.',
  'Auth.UserInactive': 'Hesabın şu anda pasif durumda.',
  'System.MaintenanceMode': 'Sistem şu anda bakımda. Biraz sonra yeniden dene.',
  'Identity.RegistrationDisabled': 'Yeni kayıtlar şu an kapalı.',
  'Registration.Failed': 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.',
  'Auth.GoogleRegistrationExpired': 'Kayıt işleminin süresi doldu. Google ile yeniden devam et.',
  'ResetPassword.InvalidRequest': 'Sıfırlama bağlantısı geçersiz ya da süresi dolmuş olabilir. Yeni bir bağlantı iste.',
  'Profiles.OwnedByCoaching': 'Eğitim profilini Koçluk profil ekranından güncelle.',
};

/** Exact server texts (trimmed) without a stable code, mostly FluentValidation messages. */
const BY_TEXT: Record<string, string> = {
  'Geçerli bir e-posta adresi giriniz.': 'Geçerli bir e-posta adresi gir.',
  'Geçerli bir telefon numarası giriniz.': 'Geçerli bir telefon numarası gir.',
  'Geçerli bir il seçiniz.': 'Geçerli bir il seç.',
  'Geçerli bir ilçe seçiniz.': 'Geçerli bir ilçe seç.',
  'Geçerli bir doğum tarihi giriniz.': 'Geçerli bir doğum tarihi gir.',
  'A valid email address is required': 'Geçerli bir e-posta adresi gir.',
  'Email address is required': 'E-posta adresini yaz.',
  'First name is required': 'Adını yaz.',
  'Last name is required': 'Soyadını yaz.',
  'Hesabınız pasif durumdadır.': 'Hesabın şu anda pasif durumda.',
  'Lütfen e-posta adresinizi doğrulayın.': 'Giriş yapmadan önce e-posta adresini doğrula.',
  'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.': 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.',
  'Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.': 'Güvenlik doğrulaması tamamlanamadı. Yeniden dene.',
  'Oturum süresi dolmuş. Lütfen tekrar giriş yapın.': 'Oturumun sona erdi. Yeniden giriş yap.',
  'Oturum anahtarı artık geçerli değil; lütfen tekrar giriş yapın.': 'Oturumun artık geçerli değil. Yeniden giriş yap.',
  'Kayıt için gerekli yasal metinlerden biri şu anda yayımlanmamış. Lütfen daha sonra tekrar deneyin.':
    'Kayıt için gerekli yasal metinlerden biri şu anda yayımlanmamış. Biraz sonra yeniden dene.',
  'Yasal belge onay bilgisi geçersiz. Sayfayı yenileyip tekrar deneyin.': 'Yasal metin onayın geçersiz. Sayfayı yenileyip yeniden dene.',
  'Kayıt işleminizin süresi doldu. Lütfen Google ile tekrar devam edin.': 'Kayıt işleminin süresi doldu. Google ile yeniden devam et.',
  'E-posta, mevcut şifre ve hesap türünü kontrol edin.': 'E-posta adresini, mevcut şifreni ve hesap türünü kontrol et.',
  'Mevcut hesabınızın e-posta adresini ve şifresini kullanın.': 'Mevcut hesabının e-posta adresini ve şifresini kullan.',
  'Mevcut hesabınızın e-posta adresini ve şifresini kullanın. Bu işlem şifrenizi değiştirmez.':
    'Mevcut hesabının e-posta adresini ve şifresini kullan. Bu işlem şifreni değiştirmez.',
  'Mevcut hesabınız aktif olmalıdır.': 'Mevcut hesabın aktif olmalı.',
  'Bu serviste kaydınız zaten var. Giriş yapın; erişim kapatılmışsa servis yöneticisiyle görüşün.':
    'Bu serviste zaten kaydın var. Giriş yap; erişimin kapatılmışsa servis yöneticisiyle görüş.',
  'Katılım başka bir istekte güncellendi. Lütfen yeniden deneyin.': 'Katılım başka bir istekte güncellendi. Yeniden dene.',
  'Diğer serviste bu hesap türüne ait aktif yetkiniz bulunmuyor. Yetki değişikliği için yöneticinizle görüşün.':
    'Diğer serviste bu hesap türüne ait aktif yetkin yok. Yetki değişikliği için yöneticinle görüş.',
  'Bu servis için mevcut kurum bağlantınız var. Yöneticinizle görüşün.': 'Bu servis için mevcut bir kurum bağlantın var. Yöneticinle görüş.',
  'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.':
    'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.',
  'Bu hesap için Google girişini önce hesap ayarlarından bağlamalısınız.': 'Bu hesap için Google girişini önce hesap ayarlarından bağlaman gerekiyor.',
  'Sıfırlama bağlantısı geçersiz veya süresi dolmuş olabilir. Lütfen yeni bir bağlantı isteyin.':
    'Sıfırlama bağlantısı geçersiz ya da süresi dolmuş olabilir. Yeni bir bağlantı iste.',
  'Doğrulama kodunun süresi dolmuş. Lütfen yeni bir kod isteyin.': 'Doğrulama kodunun süresi dolmuş. Yeni bir kod iste.',
};

/** Server texts with a variable part. */
const BY_PATTERN: Array<[RegExp, (match: RegExpExecArray) => string]> = [
  [/^Çok fazla başarısız giriş denemesi\. Lütfen (\d+) dakika sonra tekrar deneyin\.$/,
    m => `Çok fazla başarısız giriş denemesi yapıldı. ${m[1]} dakika sonra yeniden dene.`],
];

/** A validation text that still addresses the reader formally. */
const FORMAL = /(?:^|\s)(?:Lütfen|lütfen)\s|(?:giriniz|seçiniz|deneyiniz|deneyin|girin|seçin|yapın|edin|isteyin|kullanın|görüşün)[.!]?$|[a-zçğıöşü](?:ınız|iniz|unuz|ünüz)(?=$|[\s.,;:!?])/;

export const SEN_VALIDATION_FALLBACK = 'Bilgilerini kontrol edip yeniden dene.';

/**
 * The "sen" version of a server text: by code, by exact text, by pattern; a FluentValidation text that is still formal
 * becomes the generic {@link SEN_VALIDATION_FALLBACK}; anything else is returned unchanged.
 */
export function toSenServerMessage(text: string, code?: string | null, validation = false): string {
  if (code && SEN_BY_CODE[code]) return SEN_BY_CODE[code];
  const trimmed = text.trim();
  if (BY_TEXT[trimmed]) return BY_TEXT[trimmed];
  for (const [pattern, rewrite] of BY_PATTERN) {
    const match = pattern.exec(trimmed);
    if (match) return rewrite(match);
  }
  if (validation && FORMAL.test(trimmed)) return SEN_VALIDATION_FALLBACK;
  return text;
}
