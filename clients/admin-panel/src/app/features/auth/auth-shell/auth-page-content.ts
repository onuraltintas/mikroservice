import { EduivmeSite } from '../../../../../shared/eduivme-sites';

/**
 * Redesign 18.11.2026 (tuval: "Giriş: maxkoç", "Giriş: eduivme"): each address gets its own sign-in page text.
 * maxkoç on the student and parent addresses, eduivme on the teacher, institution and management addresses.
 */
export type AuthBrand = 'maxkoc' | 'eduivme';

export interface AuthPageContent {
  brand: AuthBrand;
  brandName: string;
  brandNote: string;
  logo: string;
  /**
   * What the left side shows: a sample day, a parent's weekly summary, a teacher's week, an institution's exam report
   * or a short list of what the panel does.
   */
  visual: 'today' | 'week' | 'roster' | 'report' | 'points';
  place: string;
  headline: string;
  sub: string;
  points: readonly string[];
  title: string;
  lead: string;
  signUp: { lead: string; label: string; path: string } | null;
  signUpNote: string | null;
  other: { lead: string; label: string; site: EduivmeSite } | null;
}

const MAXKOC = { brand: 'maxkoc', brandName: 'maxkoç', logo: 'brand/maxkoc.svg' } as const;
const EDUIVME = { brand: 'eduivme', brandName: 'eduivme', logo: 'brand/eduivme.svg' } as const;

export const AUTH_PAGES: Readonly<Record<EduivmeSite, AuthPageContent>> = {
  student: {
    ...MAXKOC, brandNote: 'Öğrenci paneli', visual: 'today', place: 'maxkoc.com',
    headline: 'Haftan seni bekliyor.',
    sub: 'Bugünün işleri, deneme netlerin ve koçunun notları tek yerde.',
    points: [],
    title: 'Giriş yap', lead: 'Kaldığın yerden devam et.',
    signUp: { lead: 'Hesabın yok mu?', label: 'Ücretsiz kaydol', path: '/auth/register/student' }, signUpNote: null,
    other: { lead: 'Veli misin?', label: 'Veli girişi', site: 'parent' }
  },
  parent: {
    ...MAXKOC, brandNote: 'Veli paneli', visual: 'week', place: 'veli.maxkoc.com',
    headline: 'Çocuğunun haftasını tek bakışta gör.',
    sub: 'Çalışma süresi, denemeler ve görüşmeler; yalnız çocuğunun paylaştığı kadarıyla.',
    points: [],
    title: 'Veli girişi', lead: 'Hesabın yoksa çocuğundan aldığın kodla kaydol.',
    signUp: { lead: 'Hesabın yok mu?', label: 'Veli olarak kaydol', path: '/auth/register/parent' }, signUpNote: null,
    other: { lead: 'Öğrenci misin?', label: 'Öğrenci girişi', site: 'student' }
  },
  teacher: {
    ...EDUIVME, brandNote: 'Personel paneli', visual: 'roster', place: 'ogretmen.eduivme.com',
    headline: 'Öğrencilerinin haftası tek listede.',
    sub: 'Planları, ödevleri ve denemeleri izle; kimin desteğe ihtiyacı olduğunu ilk bakışta gör.',
    points: ['maxkoç ve Master Hızlı Okuma öğrencileri', 'Ödev, seans ve haftalık değerlendirme', 'Veliyle ve öğrenciyle mesajlaşma'],
    title: 'Öğretmen girişi', lead: 'eduivme personel hesabınla devam et.',
    signUp: { lead: 'Hesabın yok mu?', label: 'Öğretmen olarak katıl', path: '/auth/register/teacher' }, signUpNote: null,
    other: { lead: 'Kurum yöneticisi misin?', label: 'Kurum girişi', site: 'institution' }
  },
  institution: {
    ...EDUIVME, brandNote: 'Personel paneli', visual: 'report', place: 'kurum.eduivme.com',
    headline: 'Kurumunun koçluk işleri tek yerde.',
    sub: 'Öğretmenler, öğrenciler, koltuklar ve tahsilat; hepsi aynı panelde.',
    points: ['Öğretmen ve öğrenci yönetimi', 'Deneme analizi ve raporlar', 'Abonelik ve tahsilat'],
    title: 'Kurum girişi', lead: 'Kurum yöneticisi hesabınla devam et.',
    signUp: { lead: 'Kurumunuzu eklemek ister misiniz?', label: 'Kurum başvurusu', path: '/auth/register/institution' }, signUpNote: null,
    other: { lead: 'Öğretmen misin?', label: 'Öğretmen girişi', site: 'teacher' }
  },
  admin: {
    ...EDUIVME, brandNote: 'Yönetim paneli', visual: 'points', place: 'admin.eduivme.com',
    headline: 'eduivme yönetim paneli',
    sub: 'Kullanıcılar, içerik, abonelikler ve sistem ayarları. Yalnız yetkili personel içindir.',
    points: ['İki adımlı doğrulama zorunlu', 'Her işlem kayıt altında', 'Yetkiler role göre sınırlı'],
    title: 'Yönetici girişi', lead: 'Giriş yaptıktan sonra doğrulama kodunu gireceksiniz.',
    signUp: null, signUpNote: 'Erişiminiz yoksa sistem yöneticinize başvurun.',
    other: null
  }
};
