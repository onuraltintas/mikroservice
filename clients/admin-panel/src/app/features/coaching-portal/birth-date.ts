/** Today ± years as YYYY-MM-DD in Türkiye (the server's birth-date window). */
export function shiftYears(years: number, now = new Date()): string {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(now);
  const [y, m, d] = today.split('-').map(Number);
  const shifted = new Date(Date.UTC(y + years, m - 1, d));
  if (shifted.getUTCMonth() !== m - 1) shifted.setUTCDate(0); // 29 Feb → 28 Feb
  return shifted.toISOString().slice(0, 10);
}

/**
 * G5 (#44): what is wrong with a birth date (YYYY-MM-DD), or null: the server's rule (Türkiye date, 6 to 100 years old). The
 * wizard and Profilim (PR 128 review L1) use it.
 */
export function birthDateError(value: string, now = new Date()): string | null {
  if (!value) return 'Doğum tarihini seç.';
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return 'Doğum tarihini gün, ay ve yıl olarak seç.';
  if (value > shiftYears(0, now)) return 'Doğum tarihi bugünden sonra olamaz.';
  if (value > shiftYears(-6, now)) return 'Bu tarihe göre 6 yaşından küçüksün. Doğum tarihini kontrol et.';
  if (value < shiftYears(-100, now)) return 'Bu tarihe göre 100 yaşından büyüksün. Doğum tarihini kontrol et.';
  return null;
}
