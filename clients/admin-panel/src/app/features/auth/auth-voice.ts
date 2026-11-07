import { DOCUMENT } from '@angular/common';
import { InjectionToken, inject } from '@angular/core';

/**
 * Paket F #12 (user decision 07.11.2026): the sign-in, sign-up, password and e-mail verification pages address the
 * user with "sen" on the Coaching site (onuraltintas.net). The same pages also serve the management panel on
 * eduivme.com, which keeps "siz". The voice follows the host: eduivme.com / eduivme.com.tr (and www.) → siz,
 * every other host (onuraltintas.net, staging, localhost) → sen.
 */
export type AuthVoice = 'sen' | 'siz';

const MANAGEMENT_HOST = /(^|\.)eduivme\.com(\.tr)?$/i;

export function authVoiceForHost(hostname: string | null | undefined): AuthVoice {
  return MANAGEMENT_HOST.test((hostname ?? '').trim().replace(/\.$/, '')) ? 'siz' : 'sen';
}

export const AUTH_VOICE = new InjectionToken<AuthVoice>('AUTH_VOICE', {
  providedIn: 'root',
  factory: () => authVoiceForHost(inject(DOCUMENT).location?.hostname)
});

export interface AuthCopy {
  // Login
  welcome: string;
  loginIntro: string;
  brandIntro: string;
  noAccount: string;
  signUp: string;
  loginSuccess: string;
  emailNotConfirmed: string;
  userInactive: string;
  userInactiveToast: string;
  loginFailed: string;
  legalAcceptanceNeeded: string;
  googleRegistrationFailed: string;
  resendAccepted: string;
  mfaSetupTitle: string;
  mfaSetupHint: string;
  mfaVerifyHint: string;
  recoveryTitle: string;
  recoveryHint: string;
  // Forgot password
  forgotIntro: string;
  forgotSentTitle: string;
  forgotSentHint: string;
  forgotFailed: string;
  // Reset password
  resetIntroSuffix: string;
  resetDoneTitle: string;
  resetDoneHint: string;
  resetDoneToast: string;
  resetFailed: string;
  // E-mail verification (/auth/confirm-email)
  verifying: string;
  verifiedTitle: string;
  verifiedHint: string;
  verifiedToast: string;
  alreadyVerifiedTitle: string;
  alreadyVerifiedHint: string;
  expiredTitle: string;
  expiredHint: string;
  invalidTitle: string;
  invalidHint: string;
  unavailableTitle: string;
  unavailableHint: string;
  resendEmailLabel: string;
  resendSent: string;
  resendFailed: string;
  // Google sign-up consent and the legal texts note (also shown on the management host's login page)
  googleConsentLabel: string;
  googleConsentTitle: string;
  googleConsentIntro: string;
  googleConsentBusy: string;
  googleConsentTimeLimit: string;
  legalNote: string;
}

export const AUTH_COPY: Record<AuthVoice, AuthCopy> = {
  sen: {
    welcome: 'Hoş geldin 👋',
    loginIntro: 'Hesabına giriş yap.',
    brandIntro: 'Çalışmanı, hedeflerini ve ilerlemeni tek bir yerden takip et.',
    noAccount: 'Hesabın yok mu?',
    signUp: 'Kayıt ol',
    loginSuccess: 'Giriş başarılı, yönlendiriliyorsun.',
    emailNotConfirmed: 'Giriş yapmadan önce e-posta adresini doğrula.',
    userInactive: 'Hesabın şu anda pasif durumda.',
    userInactiveToast: 'Hesabın pasif.',
    loginFailed: 'Giriş yapılamadı. Bilgilerini kontrol edip yeniden dene.',
    legalAcceptanceNeeded: 'Yeni hesabın için kayıt ekranındaki yasal metinleri inceleyip onayla, ardından Google ile kayıt düğmesini kullan.',
    googleRegistrationFailed: 'Kayıt tamamlanamadı. Google ile yeniden devam et.',
    // G5 (#39): the first e-mail can take a few minutes; the user is asked not to send it again needlessly.
    resendAccepted: 'Bu adres doğrulama bekliyorsa yeni bir doğrulama e-postası gönderdik. Gelen kutunu kontrol et. E-postanın gelmesi birkaç dakika sürebilir; gereksiz yere yeniden gönderme.',
    mfaSetupTitle: 'İki adımlı doğrulamayı kur',
    mfaSetupHint: 'Doğrulama uygulamana aşağıdaki anahtarı ekle ve üretilen kodu gir.',
    mfaVerifyHint: 'Doğrulama uygulamandaki altı haneli kodu gir.',
    recoveryTitle: 'Kurtarma kodlarını kaydet',
    recoveryHint: 'Bu kodlar yalnızca bir kez gösterilir. Her kod tek kullanımlıktır; güvenli bir parola yöneticisine kaydet.',
    forgotIntro: 'E-posta adresini gir, sana şifre sıfırlama bağlantısı gönderelim.',
    forgotSentTitle: 'E-postanı kontrol et!',
    forgotSentHint: 'Gelen kutunu (ve spam klasörünü) kontrol et.',
    forgotFailed: 'Sıfırlama bağlantısı şu an gönderilemedi. Biraz sonra yeniden dene.',
    resetIntroSuffix: 'hesabın için yeni şifre oluştur.',
    resetDoneTitle: 'Şifren değiştirildi! 🎉',
    resetDoneHint: 'Yeni şifrenle giriş yapabilirsin.',
    resetDoneToast: 'Şifren değiştirildi!',
    resetFailed: 'Şifre sıfırlanamadı. Yeniden dene ya da yeni bir bağlantı iste.',
    verifying: 'E-posta adresin doğrulanıyor, biraz bekle…',
    verifiedTitle: 'Tebrikler!',
    verifiedHint: 'E-posta adresin doğrulandı. Artık giriş yapabilirsin.',
    verifiedToast: 'E-posta adresin doğrulandı!',
    alreadyVerifiedTitle: 'E-posta adresin zaten doğrulanmış',
    alreadyVerifiedHint: 'Bu bağlantı daha önce kullanılmış. Hemen giriş yapabilirsin.',
    expiredTitle: 'Bağlantının süresi dolmuş',
    expiredHint: 'Doğrulama bağlantıları 24 saat geçerli. Aşağıdan yeni bir doğrulama e-postası iste.',
    invalidTitle: 'Bağlantı geçersiz',
    invalidHint: 'Bu bağlantı eksik ya da yerine yenisi gönderilmiş. En son gelen e-postadaki bağlantıyı aç ya da aşağıdan yeni bir doğrulama e-postası iste.',
    unavailableTitle: 'Doğrulama şu an tamamlanamadı',
    unavailableHint: 'Bağlantın sorunsuz olabilir; biraz sonra yeniden dene.',
    resendEmailLabel: 'E-posta adresin',
    resendSent: 'Bu adres doğrulama bekliyorsa yeni bir doğrulama e-postası gönderdik. Gelen kutunu kontrol et. E-postanın gelmesi birkaç dakika sürebilir; gereksiz yere yeniden gönderme.',
    resendFailed: 'Doğrulama e-postası şu an gönderilemedi. Biraz sonra yeniden dene.',
    googleConsentLabel: 'Google kaydını tamamla',
    googleConsentTitle: 'Kaydını tamamla',
    googleConsentIntro: 'Google hesabın doğrulandı. Hesabını oluşturmak için aşağıdaki metinleri inceleyip onayla.',
    googleConsentBusy: 'Hesabın oluşturuluyor…',
    googleConsentTimeLimit: 'Bu işlemi 5 dakika içinde tamamlaman gerekiyor. Vazgeçersen hesap oluşturulmaz.',
    legalNote: 'Onayladığın metinlerin hangi sürüm olduğu kaydınla birlikte saklanır.'
  },
  siz: {
    welcome: 'Hoş Geldiniz 👋',
    loginIntro: 'Lütfen hesabınıza giriş yapın.',
    brandIntro: 'Tüm okul süreçlerinizi tek bir modern platformda birleştirin.',
    noAccount: 'Hesabınız yok mu?',
    signUp: 'Kayıt Olun',
    loginSuccess: 'Giriş başarılı! Yönlendiriliyorsunuz.',
    emailNotConfirmed: 'Lütfen e-posta adresinizi doğrulayın.',
    userInactive: 'Hesabınız şu anda pasif durumdadır.',
    userInactiveToast: 'Hesabınız pasif.',
    loginFailed: 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.',
    legalAcceptanceNeeded: 'Yeni hesabınız için kayıt ekranındaki yasal metinleri inceleyip onaylayın, ardından Google ile kayıt düğmesini kullanın.',
    googleRegistrationFailed: 'Kayıt tamamlanamadı. Lütfen Google ile tekrar devam edin.',
    resendAccepted: 'Eğer bu adres doğrulama bekliyorsa e-posta gönderme isteğiniz alındı. Gelen kutunuzu kontrol edin. E-postanın gelmesi birkaç dakika sürebilir; lütfen gereksiz yere yeniden göndermeyin.',
    mfaSetupTitle: 'İki Adımlı Doğrulamayı Kurun',
    mfaSetupHint: 'Authenticator uygulamanıza aşağıdaki anahtarı ekleyin ve üretilen kodu girin.',
    mfaVerifyHint: 'Authenticator uygulamanızdaki altı haneli kodu girin.',
    recoveryTitle: 'Kurtarma Kodlarınızı Kaydedin',
    recoveryHint: 'Bu kodlar yalnızca bir kez gösterilir. Her kod tek kullanımlıktır; güvenli bir parola yöneticisine kaydedin.',
    forgotIntro: 'E-posta adresinizi girin, size şifre sıfırlama bağlantısı gönderelim.',
    forgotSentTitle: 'E-postanızı Kontrol Edin!',
    forgotSentHint: 'Lütfen gelen kutunuzu (ve spam klasörünü) kontrol edin.',
    forgotFailed: 'Sıfırlama bağlantısı şu an gönderilemedi. Lütfen biraz sonra yeniden deneyin.',
    resetIntroSuffix: 'hesabınız için yeni şifre oluşturun.',
    resetDoneTitle: 'Şifreniz Değiştirildi! 🎉',
    resetDoneHint: 'Yeni şifrenizle giriş yapabilirsiniz.',
    resetDoneToast: 'Şifreniz başarıyla değiştirildi!',
    resetFailed: 'Şifre sıfırlanamadı. Lütfen yeniden deneyin ya da yeni bir bağlantı isteyin.',
    verifying: 'E-posta adresiniz doğrulanıyor, lütfen bekleyin…',
    verifiedTitle: 'Tebrikler!',
    verifiedHint: 'E-posta adresiniz başarıyla doğrulandı. Artık giriş yapabilirsiniz.',
    verifiedToast: 'E-posta adresiniz doğrulandı!',
    alreadyVerifiedTitle: 'E-posta adresiniz zaten doğrulanmış',
    alreadyVerifiedHint: 'Bu bağlantı daha önce kullanılmış. Hemen giriş yapabilirsiniz.',
    expiredTitle: 'Bağlantının süresi dolmuş',
    expiredHint: 'Doğrulama bağlantıları 24 saat geçerlidir. Aşağıdan yeni bir doğrulama e-postası isteyin.',
    invalidTitle: 'Bağlantı geçersiz',
    invalidHint: 'Bu bağlantı eksik ya da yerine yenisi gönderilmiş. En son gelen e-postadaki bağlantıyı açın ya da aşağıdan yeni bir doğrulama e-postası isteyin.',
    unavailableTitle: 'Doğrulama şu an tamamlanamadı',
    unavailableHint: 'Bağlantınız sorunsuz olabilir; lütfen biraz sonra yeniden deneyin.',
    resendEmailLabel: 'E-posta adresiniz',
    resendSent: 'Eğer bu adres doğrulama bekliyorsa yeni bir doğrulama e-postası gönderdik. Gelen kutunuzu kontrol edin. E-postanın gelmesi birkaç dakika sürebilir; lütfen gereksiz yere yeniden göndermeyin.',
    resendFailed: 'Doğrulama e-postası şu an gönderilemedi. Lütfen biraz sonra yeniden deneyin.',
    googleConsentLabel: 'Google kaydını tamamlayın',
    googleConsentTitle: 'Kaydınızı tamamlayın',
    googleConsentIntro: 'Google hesabınız doğrulandı. Hesabınızı oluşturmak için aşağıdaki metinleri inceleyip onaylayın.',
    googleConsentBusy: 'Hesabınız oluşturuluyor…',
    googleConsentTimeLimit: 'Bu işlem 5 dakika içinde tamamlanmalıdır. Vazgeçerseniz hesap oluşturulmaz.',
    legalNote: 'Onayladığınız metinlerin hangi sürüm olduğu kaydınızla birlikte saklanır.'
  }
};

/** The copy for the current host. */
export function injectAuthCopy(): { voice: AuthVoice; copy: AuthCopy } {
  const voice = inject(AUTH_VOICE);
  return { voice, copy: AUTH_COPY[voice] };
}
