/**
* Paket A (05.11.2026): the identity API reports whether registration sent a verification e-mail.
* `notRequired` = an account from the other Eduİvme service whose address is already verified;
* `sent` = a new account, or an existing one whose address still has to be verified.
*/
export type VerificationEmailOutcome = 'sent' | 'notRequired';
export type RegistrationVoice = 'sen' | 'siz';

export interface RegistrationSuccessCopy {
  title: string;
  message: string;
}

/** Only an explicit `notRequired` skips the "e-posta gönderildi" screen; an older API (no field) means sent. */
export function readVerificationEmailOutcome(response: unknown): VerificationEmailOutcome {
  const value = response && typeof response === 'object'
    ? (response as { verificationEmail?: unknown }).verificationEmail
    : undefined;
  return value === 'notRequired' ? 'notRequired' : 'sent';
}

export function registrationSuccessCopy(outcome: VerificationEmailOutcome, voice: RegistrationVoice): RegistrationSuccessCopy {
  if (outcome === 'notRequired') {
    return voice === 'sen'
      ? {
        title: 'E-posta adresin zaten doğrulanmış',
        message: 'Kaydın tamamlandı. E-posta adresin zaten doğrulanmış; mevcut şifrenle hemen giriş yapabilirsin.'
      }
      : {
        title: 'E-posta adresiniz zaten doğrulanmış',
        message: 'Kaydınız tamamlandı. E-posta adresiniz zaten doğrulanmış; mevcut şifrenizle hemen giriş yapabilirsiniz.'
      };
  }
  return voice === 'sen'
    ? {
      title: 'Doğrulama e-postası gönderildi',
      message: 'Kaydın tamamlandı. E-posta adresine doğrulama bağlantısı gönderdik; bağlantıya tıkladıktan sonra giriş yapabilirsin.'
    }
    : {
      title: 'Doğrulama e-postası gönderildi',
      message: 'Kaydınız tamamlandı. E-posta adresinize doğrulama bağlantısı gönderdik; bağlantıya tıkladıktan sonra giriş yapabilirsiniz.'
    };
}
