import { SITE_ICONS, SiteBrand, SiteEntry, SiteEntryRequest, SiteIcon, SiteKind } from '../../core/services/eduivme-site.service';

/** The admin screens of "eduivme sitesi" (sidebar children). */
export type SiteAdminView = 'home' | 'products' | 'pages' | 'audiences' | 'faq' | 'media';

export const SITE_ADMIN_VIEWS: { view: SiteAdminView; label: string; description: string }[] = [
  { view: 'home', label: 'Ana sayfa', description: 'Giriş bölümü, bölüm başlıkları, alt bilgi metni ve arama motoru bilgileri.' },
  { view: 'products', label: 'Ürünler', description: 'Ana sayfadaki ürün kartları. Karta tıklanınca ürünün kendi sitesi ya da eduivme içindeki sayfası açılır.' },
  { view: 'pages', label: 'Ürün sayfaları', description: 'Kendi sitesi olmayan ürünlerin eduivme.com/urunler/… sayfaları ve bu sayfaların bölümleri.' },
  { view: 'audiences', label: 'Kimler için', description: 'Ana sayfadaki öğrenci, veli, öğretmen ve kurum kartları.' },
  { view: 'faq', label: 'Sık sorulanlar', description: 'Ana sayfadaki sorular ve yanıtları.' },
  { view: 'media', label: 'Görseller', description: 'Giriş bölümünde, ürün kartlarında ve ürün sayfalarında kullanılabilecek görseller.' }
];

export type SiteField = 'slug' | 'title' | 'subtitle' | 'body' | 'highlights' | 'brand' | 'icon' | 'link' | 'secondaryLink' | 'image' | 'cta' | 'seo';

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

export const SITE_KIND_FORMS: Record<SiteKind, SiteKindForm> = {
  site: { label: 'Site ayarları', fields: ['title', 'body', 'seo'], titleLabel: 'Site adı', bodyLabel: 'Alt bilgideki tanıtım cümlesi', single: true },
  hero: { label: 'Giriş bölümü', fields: ['title', 'body', 'link', 'secondaryLink', 'image'], titleLabel: 'Başlık', bodyLabel: 'Açıklama', linkLabel: 'Ana düğme', single: true },
  section: { label: 'Bölüm başlığı', fields: ['slug', 'title', 'body'], titleLabel: 'Başlık', bodyLabel: 'Başlığın altındaki açıklama' },
  product: { label: 'Ürün kartı', fields: ['title', 'subtitle', 'body', 'highlights', 'brand', 'link', 'image'], titleLabel: 'Ürün adı', subtitleLabel: 'Kısa tanım', bodyLabel: 'Açıklama', linkLabel: 'Karta tıklanınca' },
  audience: { label: 'Kimler için kartı', fields: ['title', 'body', 'icon', 'link'], titleLabel: 'Başlık', bodyLabel: 'Metin', linkLabel: 'Bağlantı' },
  faq: { label: 'Soru', fields: ['title', 'body'], titleLabel: 'Soru', bodyLabel: 'Yanıt' },
  page: { label: 'Ürün sayfası', fields: ['slug', 'title', 'subtitle', 'body', 'brand', 'link', 'secondaryLink', 'image', 'cta', 'seo'], titleLabel: 'Ürün adı', subtitleLabel: 'Sayfa başlığı', bodyLabel: 'Giriş metni', linkLabel: 'Ana düğme' },
  feature: { label: 'Özellik', fields: ['title', 'body', 'icon'], titleLabel: 'Özellik adı', bodyLabel: 'Açıklama' },
  step: { label: 'Adım', fields: ['title', 'body'], titleLabel: 'Adım', bodyLabel: 'Açıklama' }
};

export const BRAND_OPTIONS: { value: SiteBrand; label: string }[] = [
  { value: 'eduivme', label: 'eduivme' },
  { value: 'maxkoc', label: 'maxkoç' },
  { value: 'ho', label: 'Master Hızlı Okuma' },
  { value: 'km', label: 'Kurs Yönetimi' }
];

export const ICON_LABELS: Record<SiteIcon, string> = {
  calendar: 'Takvim', check: 'Onay', assignment: 'Ödev', payment: 'Ödeme', building: 'Bina', person: 'Kişi', chart: 'Grafik',
  book: 'Kitap', clock: 'Saat', chat: 'Mesaj', star: 'Yıldız', shield: 'Kalkan', users: 'Kişiler', target: 'Hedef', bolt: 'Şimşek', school: 'Okul'
};
export const ICON_OPTIONS = SITE_ICONS.map(value => ({ value, label: ICON_LABELS[value] }));

/** The form's working copy; highlights are edited one per line. */
export interface SiteDraft extends Omit<SiteEntryRequest, 'highlights' | 'publishAt'> {
  id: string | null;
  highlightsText: string;
  publishAtLocal: string;
}

export function emptyDraft(kind: SiteKind, group: string, sortOrder: number, slug: string | null = null): SiteDraft {
  return {
    id: null, kind, group, slug, title: '', subtitle: null, body: '', highlightsText: '', brand: kind === 'product' || kind === 'page' ? 'eduivme' : null,
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
    isPublished: entry.isPublished, publishAtLocal: toLocalInput(entry.publishAt), version: entry.version
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
    ...(draft.version !== undefined ? { version: draft.version } : {})
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
  if (entry.publishAt && new Date(entry.publishAt) > now) return { label: 'Zamanlandı', tone: 'warning' };
  return { label: 'Yayında', tone: 'success' };
}
