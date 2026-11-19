import { SiteBrand, SiteHomeView, SiteIcon, SitePageView, SitePublicEntry } from '../../../core/services/eduivme-site.service';

/**
 * eduivme.com shows this content only when site-service cannot be reached; the real content is edited in the admin
 * panel ("eduivme sitesi"). It matches the first content site-service writes (SiteSeed.cs).
 */
function entry(kind: SitePublicEntry['kind'], title: string, body: string, extra: Partial<SitePublicEntry> = {}): SitePublicEntry {
  return {
    id: `${kind}-${title}`, kind, slug: null, title, subtitle: null, body, highlights: [], brand: null, icon: null,
    linkLabel: null, linkUrl: null, secondaryLinkLabel: null, secondaryLinkUrl: null, imageUrl: null,
    ctaTitle: null, ctaBody: null, seoTitle: null, seoDescription: null, ...extra
  };
}

const SITE = entry('site', 'eduivme', 'Öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri.', {
  seoTitle: 'eduivme: öğrenmenin her adımı için eğitim ürünleri',
  seoDescription: 'maxkoç öğrenci koçluğu, Master Hızlı Okuma ve Kurs Yönetimi: öğrenciler, veliler, öğretmenler ve kurumlar için eduivme ürünleri.'
});

export const DEFAULT_HOME: SiteHomeView = {
  site: SITE,
  hero: entry('hero', 'Öğrenmenin her adımı için araçlar.', 'eduivme; öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri geliştirir. Her ürün tek bir işi iyi yapar.', {
    linkLabel: 'Ürünleri incele', linkUrl: '#urunler', secondaryLinkLabel: 'Öğretmen ya da kurum olarak başla', secondaryLinkUrl: '#kimler'
  }),
  sections: [
    entry('section', 'Ürünler', 'Her ürünün kendi sayfası var. Karta tıklayarak ürünü yakından tanıyın.', { slug: 'products' }),
    entry('section', 'Kimler için', 'İlkokuldan üniversiteye, her öğrenci ve onun yanındaki herkes için.', { slug: 'audiences' }),
    entry('section', 'Sık sorulanlar', '', { slug: 'faq' })
  ],
  products: [
    entry('product', 'maxkoç', 'Kişiye göre haftalık çalışma planı, ödevler, deneme sonuçları ve koçla düzenli görüşmeler. Veli, öğrencinin izin verdiği kadarını kendi ekranından izler.', {
      slug: 'maxkoc', brand: 'maxkoc', subtitle: 'Öğrenci koçluğu', linkLabel: 'maxkoc.com', linkUrl: 'https://maxkoc.com',
      highlights: ['Boş saatlere göre haftalık plan', 'Öğrenci, veli ve koç aynı planı görür', 'Ödev ve deneme takibi']
    }),
    entry('product', 'Master Hızlı Okuma', 'Seviye belirleme sınavıyla başlayan, her gün kısa egzersizlerle ilerleyen bir okuma programı. Hız ve anlama birlikte ölçülür.', {
      slug: 'master-hizli-okuma', brand: 'ho', subtitle: 'Okuma hızı ve anlama', linkLabel: 'masterhizliokuma.com', linkUrl: 'https://masterhizliokuma.com',
      highlights: ['İlkokuldan yetişkine her seviye için', 'Günde birkaç dakikalık egzersizler', 'İlerleme grafikleri ve rozetler']
    }),
    entry('product', 'Kurs Yönetimi', 'Dersler, gruplar, yoklama, ödevler ve öğrenci ödemeleri tek yerde. Öğrenciler derslerini ve ödevlerini kendi alanından izler.', {
      slug: 'kurs-yonetimi', brand: 'km', subtitle: 'Kurs ve özel ders yönetimi', linkLabel: 'Ürün sayfasına git', linkUrl: '/urunler/kurs-yonetimi',
      highlights: ['Haftalık ders programı ve yoklama', 'Ödev verme ve değerlendirme', 'Taksit ve paket takibi']
    })
  ],
  audiences: [
    entry('audience', 'Öğrenci', 'Planını, ödevlerini ve egzersizlerini tek yerden takip et.', { icon: 'school', linkLabel: 'Öğrenci girişi', linkUrl: 'https://maxkoc.com/auth/login' }),
    entry('audience', 'Veli', 'Çocuğunun çalışmasını, onun izin verdiği kadarıyla izle.', { icon: 'users', linkLabel: 'Veli girişi', linkUrl: 'https://veli.maxkoc.com/auth/login' }),
    entry('audience', 'Öğretmen', 'Öğrencilerinizi davet edin, ödev verin, ilerlemeyi görün.', { icon: 'person', linkLabel: 'Öğretmen girişi', linkUrl: 'https://ogretmen.eduivme.com/auth/login' }),
    entry('audience', 'Kurum', 'Öğretmenlerinizi ve öğrencilerinizi tek panelden yönetin.', { icon: 'building', linkLabel: 'Kurum girişi', linkUrl: 'https://kurum.eduivme.com/auth/login' })
  ],
  faqs: [
    entry('faq', 'eduivme nedir?', 'eduivme; maxkoç, Master Hızlı Okuma ve Kurs Yönetimi ürünlerinin çatı markasıdır. Her ürünün kendi sitesi ve kendi girişi vardır.'),
    entry('faq', 'Hangi ürün bana uygun?', 'Düzenli bir çalışma planı ve koç desteği istiyorsanız maxkoç, okuma hızınızı ve anlamanızı geliştirmek istiyorsanız Master Hızlı Okuma, kurs ya da özel ders veriyorsanız Kurs Yönetimi size göre. Ürünleri birlikte de kullanabilirsiniz.'),
    entry('faq', 'Öğretmen ya da kurum olarak nasıl başlarım?', 'Öğretmen girişinden ya da kurum girişinden hesap açın. Hesabınız açıldıktan sonra öğrencilerinizi davet eder, ödev ve programlarını oradan yönetirsiniz.'),
    entry('faq', 'Kişisel verilerim nasıl korunuyor?', 'Kişisel verilerin hangi amaçla ve nasıl işlendiği KVKK aydınlatma metninde ve gizlilik politikasında anlatılır. maxkoç\'ta veli, öğrencinin yalnız izin verdiği bilgileri görür.')
  ]
};

export const DEFAULT_PAGES: Record<string, SitePageView> = {
  'kurs-yonetimi': {
    site: SITE,
    page: entry('page', 'Kurs Yönetimi', 'Ders programı, yoklama, ödevler ve öğrenci ödemeleri bir arada. Öğretmenleriniz derslerine, öğrencileriniz kendi alanlarına odaklanır.', {
      slug: 'kurs-yonetimi', brand: 'km', subtitle: 'Kursunuzu tek ekrandan yönetin.',
      linkLabel: 'Hemen başla', linkUrl: 'https://ogretmen.eduivme.com/auth/register/teacher',
      secondaryLinkLabel: 'Giriş yap', secondaryLinkUrl: 'https://ogretmen.eduivme.com/auth/login',
      ctaTitle: 'Kursunuzu bugün kurun.', ctaBody: 'Hesap açmak ücretsiz.',
      seoTitle: 'Kurs Yönetimi: ders programı, yoklama, ödev ve ödeme takibi | eduivme',
      seoDescription: 'Kurslar, dershaneler ve özel ders veren öğretmenler için ders programı, yoklama, ödev ve öğrenci ödemeleri tek yerde.'
    }),
    sections: [
      entry('section', 'Neler yapabilirsiniz', 'Kursun günlük işleri için gereken her şey, ayrı ayrı araçlara gerek kalmadan.', { slug: 'features' }),
      entry('section', 'Kimler için', '', { slug: 'audiences' }),
      entry('section', 'Nasıl başlanır', 'Kurulum birkaç dakika sürer.', { slug: 'steps' }),
      entry('section', 'Sık sorulanlar', '', { slug: 'faq' })
    ],
    features: [
      entry('feature', 'Ders programı', 'Tek seferlik dersler ve haftalık seriler. Öğretmen ve derslik çakışmaları kayıttan önce yakalanır.', { icon: 'calendar' }),
      entry('feature', 'Yoklama ve telafi', 'Her derste yoklama alın; kaçan dersler için telafi hakkı tanımlayın.', { icon: 'check' }),
      entry('feature', 'Ödevler', 'Bir öğrenciye ya da gruba ödev verin, dosya ekleyin, teslimleri değerlendirin.', { icon: 'assignment' }),
      entry('feature', 'Öğrenci ödemeleri', 'Nakit ve havale tahsilatı, taksit ve ders paketi takibi; gecikenler tek listede.', { icon: 'payment' }),
      entry('feature', 'Şube ve derslikler', 'Birden çok şube ve derslik; kapasite derse göre denetlenir.', { icon: 'building' }),
      entry('feature', 'Öğrenci alanı', 'Öğrenci derslerini, yoklamasını, ödevlerini ve bakiyesini kendi ekranından görür.', { icon: 'person' })
    ],
    audiences: [
      entry('audience', 'Kurslar ve dershaneler', 'Birden çok öğretmen, grup ve şube. Kurum yöneticisi bütün dersleri, yoklamaları ve tahsilatları görür.', { icon: 'building' }),
      entry('audience', 'Özel ders veren öğretmenler', 'Bire bir ve küçük grup dersleri, ders paketleri ve ödevler; tek kişilik bir çalışma alanı.', { icon: 'person' })
    ],
    steps: [
      entry('step', 'Hesap açın', 'Kurum ya da özel ders veren öğretmen olarak ücretsiz kaydolun.'),
      entry('step', 'Kursunuzu kurun', 'Derslerinizi, gruplarınızı ve ders programınızı ekleyin.'),
      entry('step', 'Öğrencilerinizi davet edin', 'Öğrenciler davetle katılır; derslerini ve ödevlerini kendi alanından izler.')
    ],
    faqs: [
      entry('faq', 'Kurs Yönetimi kimler için?', 'Kurslar, dershaneler, etüt merkezleri ve özel ders veren öğretmenler için. Tek öğretmenle de, birden çok şubeyle de kullanılabilir.'),
      entry('faq', 'Öğrencilerin hesap açması gerekiyor mu?', 'Hayır. Öğrencilerinizi hesap açmadan da kaydedebilir, derslerini ve yoklamalarını tutabilirsiniz. Ödevlerini ve programlarını kendileri görmek isteyen öğrenciler, gönderdiğiniz davetle hesap açar.'),
      entry('faq', 'Ödemeleri sistem üzerinden alabilir miyim?', 'Nakit ya da havale ile aldığınız ödemeleri kaydedersiniz; taksitler, ders paketleri ve geciken ödemeler otomatik izlenir. Öğrenci kendi bakiyesini görür. Kartla çevrim içi ödeme şu an yok.')
    ]
  }
};

/** The mark file and the colour scope of each brand a card can carry. */
export const BRAND_MARKS: Record<SiteBrand, { mark: string; scope: 'eduivme' | 'maxkoc' | 'ho' }> = {
  eduivme: { mark: '/brand/eduivme.svg', scope: 'eduivme' },
  maxkoc: { mark: '/brand/maxkoc.svg', scope: 'maxkoc' },
  ho: { mark: '/brand/master-hizli-okuma.svg', scope: 'ho' },
  km: { mark: '/brand/kurs-yonetimi.svg', scope: 'eduivme' }
};

/** Stroke icons (24 px grid) the admin picks by name. */
export const SITE_ICON_PATHS: Record<SiteIcon, string> = {
  calendar: 'M8 3v3M16 3v3M4 9h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  assignment: 'M7 3h7l5 5v13H7zM14 3v5h5M10 13h6M10 17h6',
  payment: 'M3 7h18v10H3zM3 11h18M7 15h3',
  building: 'M4 20V8l8-4 8 4v12M9 20v-6h6v6',
  person: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0',
  chart: 'M4 20V10M10 20V4M16 20v-8M22 20H2',
  book: 'M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2zM4 19V5M8 7h7',
  clock: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2',
  chat: 'M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.4A8 8 0 1 1 21 12z',
  star: 'M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z',
  shield: 'M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6z',
  users: 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 3.5a4 4 0 0 1 0 7M22 21a7 7 0 0 0-4-6.3',
  target: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10zM12 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2z',
  bolt: 'M13 2L4 14h7l-1 8 9-12h-7z',
  school: 'M2 9l10-5 10 5-10 5zM6 11v5c3 2.5 9 2.5 12 0v-5M22 9v6'
};

/** The colour scope each audience card uses, so the four cards are told apart (and also by their icon and title). */
export const AUDIENCE_SCOPES: readonly ('ho' | 'maxkoc' | 'eduivme')[] = ['ho', 'maxkoc', 'eduivme', 'eduivme'];

export type SiteLink = { kind: 'route'; path: string } | { kind: 'anchor'; fragment: string } | { kind: 'external'; href: string };

/** A path on the site goes through the router; "#x" scrolls on the page; anything else is a normal link. */
export function siteLink(url: string | null | undefined): SiteLink | null {
  if (!url) return null;
  if (url.startsWith('#')) return { kind: 'anchor', fragment: url.slice(1) };
  if (url.startsWith('/') && !url.startsWith('//')) return { kind: 'route', path: url };
  if (/^https:\/\//i.test(url)) return { kind: 'external', href: url };
  return null;
}

export function sectionOf(sections: SitePublicEntry[], slug: string): SitePublicEntry | undefined {
  return sections.find(section => section.slug === slug);
}
