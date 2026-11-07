/**
 * Paket A (05.11.2026): the identity API reports whether registration sent a verification e-mail.
 * `notRequired` = an account from the other Eduİvme service whose address is already verified;
 * `sent` = a new account, or an existing one whose address still has to be verified.
 */
export type VerificationEmailOutcome = 'sent' | 'notRequired';
export type RegistrationVoice = 'sen' | 'siz';

/**
 * G5 (#39): the first verification e-mail can take a few minutes (the recipient's mail server may hold a first message from a
 * new sender); the screens that say it was sent ask the user not to send it again needlessly.
 */
export const VERIFICATION_DELAY_NOTE: Record<RegistrationVoice, string> = {
    sen: 'E-postanın gelmesi birkaç dakika sürebilir; gereksiz yere yeniden gönderme.',
    siz: 'E-postanın gelmesi birkaç dakika sürebilir; lütfen gereksiz yere yeniden göndermeyin.'
};

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

/** Third review N3: false only when the API says this request granted nothing new (a retry); older APIs count. */
export function grantedNewProductAccess(response: unknown): boolean {
    return !(response && typeof response === 'object'
        && (response as { newProductAccess?: unknown }).newProductAccess === false);
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
            message: `Kaydın tamamlandı. E-posta adresine doğrulama bağlantısı gönderdik; bağlantıya tıkladıktan sonra giriş yapabilirsin. ${VERIFICATION_DELAY_NOTE.sen}`
        }
        : {
            title: 'Doğrulama e-postası gönderildi',
            message: `Kaydınız tamamlandı. E-posta adresinize doğrulama bağlantısı gönderdik; bağlantıya tıkladıktan sonra giriş yapabilirsiniz. ${VERIFICATION_DELAY_NOTE.siz}`
        };
}
