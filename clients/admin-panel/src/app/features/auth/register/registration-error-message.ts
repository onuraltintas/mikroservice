const validationFields: Record<string, string> = {
  email: 'e-posta',
  password: 'şifre',
  firstname: 'ad',
  lastname: 'soyad',
  institutionname: 'kurum adı',
  institutiontype: 'kurum türü',
  provinceid: 'il',
  districtid: 'ilçe',
  phonenumber: 'telefon numarası',
  branch: 'branş'
};

/** Returns safe, actionable Turkish feedback for the public registration forms. */
export function getRegistrationErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') {
    return 'Kayıt tamamlanamadı. Lütfen yeniden deneyin.';
  }

  const response = error as { status?: unknown; error?: unknown };
  const body = response.error && typeof response.error === 'object'
    ? response.error as Record<string, unknown>
    : {};

  if (response.status === 0) {
    return 'Bağlantı kurulamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.';
  }
  if (response.status === 429) {
    return 'Çok fazla deneme yapıldı. Lütfen biraz bekleyip yeniden deneyin.';
  }
  if (typeof response.status === 'number' && response.status >= 500) {
    return 'Kayıt şu anda tamamlanamıyor. Lütfen daha sonra yeniden deneyin.';
  }
  if (body['code'] === 'Identity.UserExists') {
    return 'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Giriş yapın veya şifrenizi sıfırlayın.';
  }
  if (body['code'] === 'Identity.RegistrationDisabled') {
    return 'Yeni kullanıcı kayıtları şu an kapalı. Lütfen daha sonra tekrar deneyin.';
  }
  if (body['code'] === 'Institution.InvalidLocation') {
    return 'Seçtiğiniz il ve ilçe eşleşmiyor. Lütfen konum bilgilerini kontrol edin.';
  }
  if (response.status === 409) {
    return 'Kayıt bilgileri mevcut bir kayıtla çakışıyor. Bilgileri kontrol edin.';
  }
  if (response.status === 400) {
    const errors = body['errors'];
    if (errors && typeof errors === 'object' && !Array.isArray(errors)) {
      const field = Object.keys(errors)[0]?.toLowerCase();
      if (field && validationFields[field]) {
        return `Lütfen ${validationFields[field]} alanını kontrol edin.`;
      }
    }
    return 'Kayıt bilgilerini kontrol edip yeniden deneyin.';
  }
  return 'Kayıt tamamlanamadı. Lütfen yeniden deneyin.';
}
