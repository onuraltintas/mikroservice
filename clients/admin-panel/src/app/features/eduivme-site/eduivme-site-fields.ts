import { NoticeAudience, SITE_ICONS, SiteBrand, SiteEntry, SiteEntryRequest, SiteIcon, SiteKey, SiteKind } from '../../core/services/eduivme-site.service';

/** The admin screens of "eduivme sitesi" and "maxkampüs (Kurs Yönetimi) › Site içeriği" (sidebar children). */
export type SiteAdminView = 'home' | 'products' | 'pages' | 'audiences' | 'faq' | 'media'
  | 'features' | 'reviews' | 'blog' | 'menus' | 'plans' | 'notices';

export interface SiteAdminViewInfo { view: SiteAdminView; label: string; description: string; }

/** What each site is called in the panel and where it lives. */
export const SITE_INFO: Record<SiteKey, { name: string; url: string; host: string; routeBase: string }> = {
  eduivme: { name: 'eduivme sitesi', url: 'https://eduivme.com', host: 'eduivme.com', routeBase: '/dashboard/eduivme' },
  maxkampus: { name: 'maxkampüs sitesi', url: 'https://maxkampus.com', host: 'maxkampus.com', routeBase: '/dashboard/maxkampus/site' }
};

export const MAXKAMPUS_ADMIN_VIEWS: SiteAdminViewInfo[] = [
  { view: 'home', label: 'Ana sayfa', description: 'Giriş bölümü, bölüm başlıkları, alt bilgi metni ve arama motoru bilgileri.' },
  { view: 'features', label: 'Özellikler ve adımlar', description: 'Ana sayfadaki özellik kartları ve "Nasıl başlanır" adımları.' },
  { view: 'reviews', label: 'Müşteri yorumları', description: 'Kurumların sözleri. Yalnız izin aldığınız yorumları, kişinin ve kurumun onayladığı adla ekleyin.' },
  { view: 'faq', label: 'Sık sorulanlar', description: 'Ana sayfadaki sorular ve yanıtları.' },
  { view: 'blog', label: 'Blog', description: 'maxkampus.com/blog yazıları. Yayın zamanını ileriye kurarak yazıyı zamanlayabilirsiniz.' },
  { view: 'pages', label: 'Sayfalar', description: 'maxkampus.com/sayfa/… içerik sayfaları (hakkımızda gibi) ve iletişim ile fiyatlar sayfalarının metinleri.' },
  { view: 'plans', label: 'Fiyat sayfası', description: 'Plan adları ve fiyatlar Abonelik ve ödeme › Planlar ekranından okunur. Burada yalnız açıklama ve "en çok tercih edilen" işareti girilir.' },
  { view: 'menus', label: 'Menüler', description: 'Sitenin üst menüsü ve alt bilgideki bağlantılar.' },
  { view: 'notices', label: 'Panel duyuruları', description: 'Kurum panelinin ilk sayfasında görünen duyurular: herkese, kurum sahiplerine ya da öğretmenlere; başlangıç ve bitiş tarihiyle.' },
  { view: 'media', label: 'Görseller', description: 'Giriş bölümünde, yazılarda ve sayfalarda kullanılabilecek görseller (iki site ortak kullanır).' }
];

export const SITE_ADMIN_VIEWS: SiteAdminViewInfo[] = [
  { view: 'home', label: 'Ana sayfa', description: 'Giriş bölümü, bölüm başlıkları, alt bilgi metni ve arama motoru bilgileri.' },
  { view: 'products', label: 'Ürünler', description: 'Ana sayfadaki ürün kartları. Karta tıklanınca ürünün kendi sitesi ya da eduivme içindeki sayfası açılır.' },
  { view: 'pages', label: 'Ürün sayfaları', description: 'Kendi sitesi olmayan ürünlerin eduivme.com/urunler/… sayfaları ve bu sayfaların bölümleri.' },
  { view: 'audiences', label: 'Kimler için', description: 'Ana sayfadaki öğrenci, veli, öğretmen ve kurum kartları.' },
  { view: 'faq', label: 'Sık sorulanlar', description: 'Ana sayfadaki sorular ve yanıtları.' },
  { view: 'media', label: 'Görseller', description: 'Giriş bölümünde, ürün kartlarında ve ürün sayfalarında kullanılabilecek görseller.' }
];

export type SiteField = 'slug' | 'title' | 'subtitle' | 'body' | 'highlights' | 'brand' | 'icon' | 'link' | 'secondaryLink' | 'image' | 'cta' | 'seo'
  | 'category' | 'menuPlace' | 'audience' | 'endsAt' | 'author' | 'plan';

export interface SiteKindForm {
  label: string;
  fields: SiteField[];
  titleLabel: string;
  subtitleLabel?: string;
  bodyLabel?: string;
  linkLabel?: string;
  /** Only one of these may exist in a group (site settings, opening block). */
  single?: boolean;
  /** Sections take their slug from a fixed list. */
  sectionSlugs?: { slug: string; label: string }[];
  /** Long text (blog posts, content pages): 40 000 characters. */
  long?: boolean;
}

export const HOME_SECTIONS = [
  { slug: 'products', label: 'Ürünler bölümü' },
  { slug: 'audiences', label: 'Kimler için bölümü' },
  { slug: 'faq', label: 'Sık sorulanlar bölümü' }
];
export const PAGE_SECTIONS = [
  { slug: 'features', label: 'Özellikler bölümü' },
  { slug: 'audiences', label: 'Kimler için bölümü' },
  { slug: 'steps', label: 'Nasıl başlanır bölümü' },
  { slug: 'faq', label: 'Sık sorulanlar bölümü' }
];

export const MAXKAMPUS_SECTIONS = [
  { slug: 'features', label: 'Özellikler bölümü' },
  { slug: 'steps', label: 'Nasıl başlanır bölümü' },
  { slug: 'reviews', label: 'Müşteri yorumları bölümü' },
  { slug: 'pricing', label: 'Fiyatlar bölümü' },
  { slug: 'faq', label: 'Sık sorulanlar bölümü' },
  { slug: 'blog', label: 'Blog bölümü' },
  { slug: 'cta', label: 'Sayfa sonundaki çağrı' }
];

export const NOTICE_AUDIENCES: { value: NoticeAudience; label: string }[] = [
  { value: 'all', label: 'Herkes' },
  { value: 'owners', label: 'Kurum sahipleri' },
  { value: 'teachers', label: 'Öğretmenler' }
];
export const MENU_PLACES = [{ value: 'header', label: 'Üst menü' }, { value: 'footer', label: 'Alt bilgi' }];

/** Sections each site and group may have. */
export function sectionsFor(site: SiteKey, group: string) {
  return site === 'maxkampus' ? MAXKAMPUS_SECTIONS : group === '' ? HOME_SECTIONS : PAGE_SECTIONS;
}

export const SITE_KIND_FORMS: Record<SiteKind, SiteKindForm> = {
  site: { label: 'Site ayarları', fields: ['title', 'body', 'seo'], titleLabel: 'Site adı', bodyLabel: 'Alt bilgideki tanıtım cümlesi', single: true },
  hero: { label: 'Giriş bölümü', fields: ['title', 'body', 'link', 'secondaryLink', 'image'], titleLabel: 'Başlık', bodyLabel: 'Açıklama', linkLabel: 'Ana düğme', single: true },
  section: { label: 'Bölüm başlığı', fields: ['slug', 'title', 'body', 'link'], titleLabel: 'Başlık', bodyLabel: 'Başlığın altındaki açıklama', linkLabel: 'Bağlantı (yalnız ana sayfadaki Ürünler bölümünde görünür)' },
  product: { label: 'Ürün kartı', fields: ['title', 'subtitle', 'body', 'highlights', 'brand', 'link', 'image'], titleLabel: 'Ürün adı', subtitleLabel: 'Kısa tanım', bodyLabel: 'Açıklama', linkLabel: 'Karta tıklanınca' },
  audience: { label: 'Kimler için kartı', fields: ['title', 'body', 'icon', 'link'], titleLabel: 'Başlık', bodyLabel: 'Metin', linkLabel: 'Bağlantı' },
  faq: { label: 'Soru', fields: ['title', 'body'], titleLabel: 'Soru', bodyLabel: 'Yanıt' },
  page: { label: 'Ürün sayfası', fields: ['slug', 'title', 'subtitle', 'body', 'brand', 'link', 'secondaryLink', 'image', 'cta', 'seo'], titleLabel: 'Ürün adı', subtitleLabel: 'Sayfa başlığı', bodyLabel: 'Giriş metni', linkLabel: 'Ana düğme' },
  feature: { label: 'Özellik', fields: ['title', 'body', 'icon'], titleLabel: 'Özellik adı', bodyLabel: 'Açıklama' },
  step: { label: 'Adım', fields: ['title', 'body'], titleLabel: 'Adım', bodyLabel: 'Açıklama' },
  review: { label: 'Müşteri yorumu', fields: ['title', 'subtitle', 'body', 'image'], titleLabel: 'Yorumu yapan kişi', subtitleLabel: 'Kurum ve şehir', bodyLabel: 'Yorum' },
  post: { label: 'Blog yazısı', fields: ['slug', 'title', 'subtitle', 'body', 'category', 'author', 'image', 'seo'], titleLabel: 'Başlık', subtitleLabel: 'Kısa özet (listede görünür)', bodyLabel: 'Yazı', long: true },
  menu: { label: 'Menü bağlantısı', fields: ['menuPlace', 'title', 'link'], titleLabel: 'Görünen yazı', linkLabel: 'Bağlantı' },
  notice: { label: 'Panel duyurusu', fields: ['audience', 'title', 'body', 'link', 'endsAt'], titleLabel: 'Başlık', bodyLabel: 'Duyuru metni', linkLabel: 'Bağlantı (isteğe bağlı)' },
  plan: { label: 'Plan açıklaması', fields: ['plan', 'title', 'body', 'highlights', 'category'], titleLabel: 'Kartta görünen ad', bodyLabel: 'Kısa açıklama' }
};

/** maxkampus.com content pages are long and have no product colour. */
export function formFor(kind: SiteKind, site: SiteKey): SiteKindForm {
  const form = SITE_KIND_FORMS[kind];
  if (site !== 'maxkampus') return form;
  if (kind === 'page') return { label: 'Sayfa', fields: ['slug', 'title', 'subtitle', 'body', 'image', 'seo'], titleLabel: 'Sayfa adı', subtitleLabel: 'Başlığın altındaki cümle', bodyLabel: 'Sayfa metni', long: true };
  if (kind === 'site') return { ...form, fields: ['title', 'subtitle', 'body', 'seo'], subtitleLabel: 'Marka satırı (örnek: bir eduivme markası)' };
  if (kind === 'section') return { ...form, fields: ['slug', 'title', 'body', 'link'], linkLabel: 'Düğme (yalnız sayfa sonundaki çağrıda)' };
  if (kind === 'hero') return { ...form, fields: ['title', 'subtitle', 'body', 'link', 'secondaryLink', 'image'], subtitleLabel: 'Başlığın üstündeki kısa etiket' };
  return form;
}

export const BRAND_OPTIONS: { value: SiteBrand; label: string }[] = [
  { value: 'eduivme', label: 'eduivme' },
  { value: 'maxkoc', label: 'maxkoç' },
  { value: 'ho', label: 'Master Hızlı Okuma' },
  { value: 'km', label: 'Kurs Yönetimi' }
];

export const ICON_LABELS: Record<SiteIcon, string> = {
  calendar: 'Takvim', check: 'Onay', assignment: 'Ödev', payment: 'Ödeme', building: 'Bina', person: 'Kişi', chart: 'Grafik',
  book: 'Kitap', clock: 'Saat', chat: 'Mesaj', star: 'Yıldız', shield: 'Kalkan', users: 'Kişiler', target: 'Hedef', bolt: 'Şimşek', school: 'Okul',
  mail: 'E-posta'
};
export const ICON_OPTIONS = SITE_ICONS.map(value => ({ value, label: ICON_LABELS[value] }));

/** The form's working copy; highlights are edited one per line. */
export interface SiteDraft extends Omit<SiteEntryRequest, 'highlights' | 'publishAt' | 'endsAt'> {
  id: string | null;
  highlightsText: string;
  publishAtLocal: string;
  endsAtLocal: string;
}

export function emptyDraft(kind: SiteKind, group: string, sortOrder: number, slug: string | null = null, site: SiteKey = 'eduivme'): SiteDraft {
  return {
    id: null, site, category: kind === 'menu' ? 'header' : null, audience: kind === 'notice' ? 'all' : null, author: null, endsAtLocal: '',
    kind, group, slug, title: '', subtitle: null, body: '', highlightsText: '', brand: (kind === 'product' || kind === 'page') && site === 'eduivme' ? 'eduivme' : null,
    icon: kind === 'audience' || kind === 'feature' ? 'star' : null, linkLabel: null, linkUrl: null, secondaryLinkLabel: null, secondaryLinkUrl: null,
    imageId: null, ctaTitle: null, ctaBody: null, seoTitle: null, seoDescription: null, sortOrder, isPublished: true, publishAtLocal: ''
  };
}

export function draftFrom(entry: SiteEntry): SiteDraft {
  return {
    id: entry.id, kind: entry.kind, group: entry.group, slug: entry.slug, title: entry.title, subtitle: entry.subtitle, body: entry.body,
    highlightsText: entry.highlights.join('\n'), brand: entry.brand, icon: entry.icon, linkLabel: entry.linkLabel, linkUrl: entry.linkUrl,
    secondaryLinkLabel: entry.secondaryLinkLabel, secondaryLinkUrl: entry.secondaryLinkUrl, imageId: entry.imageId, ctaTitle: entry.ctaTitle,
    ctaBody: entry.ctaBody, seoTitle: entry.seoTitle, seoDescription: entry.seoDescription, sortOrder: entry.sortOrder,
    isPublished: entry.isPublished, publishAtLocal: toLocalInput(entry.publishAt), version: entry.version,
    site: entry.site ?? 'eduivme', category: entry.category ?? null, audience: entry.audience ?? null, author: entry.author ?? null,
    endsAtLocal: toLocalInput(entry.endsAt ?? null)
  };
}

const blank = (value: string | null | undefined) => (value ?? '').trim() || null;

export function requestFrom(draft: SiteDraft): SiteEntryRequest {
  return {
    kind: draft.kind, group: draft.group, slug: blank(draft.slug), title: draft.title.trim(), subtitle: blank(draft.subtitle), body: (draft.body ?? '').trim(),
    highlights: draft.highlightsText.split('\n').map(line => line.trim()).filter(Boolean),
    brand: draft.brand, icon: draft.icon, linkLabel: blank(draft.linkLabel), linkUrl: blank(draft.linkUrl),
    secondaryLinkLabel: blank(draft.secondaryLinkLabel), secondaryLinkUrl: blank(draft.secondaryLinkUrl), imageId: draft.imageId,
    ctaTitle: blank(draft.ctaTitle), ctaBody: blank(draft.ctaBody), seoTitle: blank(draft.seoTitle), seoDescription: blank(draft.seoDescription),
    sortOrder: Number(draft.sortOrder) || 0, isPublished: draft.isPublished,
    publishAt: draft.publishAtLocal ? new Date(draft.publishAtLocal).toISOString() : null,
    ...(draft.version !== undefined ? { version: draft.version } : {}),
    site: draft.site ?? 'eduivme', category: blank(draft.category), audience: draft.audience ?? null, author: blank(draft.author),
    endsAt: draft.endsAtLocal ? new Date(draft.endsAtLocal).toISOString() : null
  };
}

function toLocalInput(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Published now, waiting for its time, or not on the site. */
export function publishState(entry: SiteEntry, now = new Date()): { label: string; tone: 'success' | 'warning' | 'muted' } {
  if (!entry.isPublished) return { label: 'Yayında değil', tone: 'muted' };
  if (entry.endsAt && new Date(entry.endsAt) <= now) return { label: 'Süresi bitti', tone: 'muted' };
  if (entry.publishAt && new Date(entry.publishAt) > now) return { label: 'Zamanlandı', tone: 'warning' };
  return { label: 'Yayında', tone: 'success' };
}
