export interface HomeSeoContent {
  title: string;
  description: string;
  keywords: string;
  ogImage: string;
}

export interface HomeHeroContent {
  title: string;
  subtitle: string;
  primaryActionLabel: string;
  secondaryActionLabel: string;
  trustPoints: string[];
}

export interface HomeFeatureContent {
  icon: string;
  title: string;
  description: string;
}

export interface HomeApproachContent {
  title: string;
  role: string;
  description: string;
}

export interface HomeSectionHeading {
  title: string;
  subtitle: string;
  actionLabel?: string;
  smallText?: string;
}

export interface HomePageContent {
  seo: HomeSeoContent;
  hero: HomeHeroContent;
  features: HomeSectionHeading & { items: HomeFeatureContent[] };
  approach: HomeSectionHeading & { items: HomeApproachContent[] };
  pricing: HomeSectionHeading;
  blog: HomeSectionHeading;
  newsletter: HomeSectionHeading & { benefits: string[] };
  faq: HomeSectionHeading;
  cta: HomeSectionHeading;
  visibility: Record<'features' | 'approach' | 'stats' | 'pricing' | 'blog' | 'newsletter' | 'faq' | 'cta', boolean>;
}

export const DEFAULT_HOME_PAGE_CONTENT: HomePageContent = {
  seo: {
    title: 'Master Hızlı Okuma | Hız ve Anlamayı Birlikte Geliştirin',
    description: 'Başlangıç düzeyinizi ölçün, size uygun çalışmaları takip edin ve gelişiminizi hız ile anlama verileriyle görün.',
    keywords: 'hızlı okuma, okuduğunu anlama, okuma egzersizleri, kişisel öğrenme planı',
    ogImage: ''
  },
  hero: {
    title: 'Hız ve anlamayı birlikte geliştirin',
    subtitle: 'Başlangıç ölçümünüzden sonra size uygun çalışmalarla ilerleyin; gelişiminizi düzenli verilerle görün.',
    primaryActionLabel: 'Seviyeni belirle',
    secondaryActionLabel: 'Nasıl çalışır?',
    trustPoints: ['Başlangıç ölçümü', 'Hız ve anlama birlikte', 'Kişisel çalışma akışı']
  },
  features: {
    title: 'Çalışma akışında neler var?',
    subtitle: 'Odak, akıcılık ve anlama çalışmalarını aynı öğrenme yolunda birleştirin.',
    items: [
      { icon: 'speed', title: 'Akıcılık çalışmaları', description: 'Metin takibi ve kelime gruplama ile daha akıcı okuyun.' },
      { icon: 'quiz', title: 'Anlama kontrolü', description: 'Her çalışmada anlama sonucunu ayrı olarak görün.' },
      { icon: 'route', title: 'Kişisel sıradaki adım', description: 'Ölçüm geçmişinize göre uygun içerikle devam edin.' },
      { icon: 'insights', title: 'İlerleme görünümü', description: 'Hız, anlama ve düzenli çalışma eğilimini izleyin.' }
    ]
  },
  approach: {
    title: 'Ölçerek ilerleyen bir çalışma düzeni',
    subtitle: 'Program, tek bir hız hedefine değil, sürdürülebilir gelişime odaklanır.',
    items: [
      { title: 'Başlangıcı görün', role: 'Ölçüm', description: 'Çalışma yolunuz başlangıç ölçümünüzle netleşir.' },
      { title: 'Dengeli ilerleyin', role: 'Hız + anlama', description: 'Zorluk, sonuçlarınıza göre dengelenir.' },
      { title: 'Geri bildirimi kullanın', role: 'İzleme', description: 'Güçlü yönlerinizi ve destek ihtiyacını görün.' }
    ]
  },
  pricing: { title: 'Size uygun erişimi seçin', subtitle: 'Bireysel veya kurumsal erişim seçeneklerini inceleyin.' },
  blog: { title: 'Kaynaklar ve çalışma ipuçları', subtitle: 'Okuma, öğrenme ve düzenli çalışma üzerine içerikleri keşfedin.', actionLabel: 'Tüm yazıları gör' },
  newsletter: {
    title: 'Çalışma ipuçları e-postanıza gelsin',
    subtitle: 'Yeni içeriklerden ve yararlı çalışma önerilerinden haberdar olun.',
    benefits: ['Yeni çalışma önerileri', 'Güncel içerikler', 'İstediğiniz zaman abonelikten çıkma özgürlüğü']
  },
  faq: { title: 'Sık sorulan sorular', subtitle: 'Platform ve çalışma düzeni hakkında kısa yanıtlar.', actionLabel: 'Tüm soruları gör' },
  cta: {
    title: 'Çalışma yolunuzu görmek ister misiniz?',
    subtitle: 'Seviyenizi belirleyip size uygun ilk adımı görün.',
    actionLabel: 'Başla',
    smallText: 'Sonuçlar başlangıç düzeyine ve düzenli çalışmaya göre değişir.'
  },
  visibility: { features: true, approach: true, stats: true, pricing: true, blog: true, newsletter: true, faq: true, cta: true }
};

type CmsValue = Record<string, unknown>;

export function parseHomePageContent(blocks: Record<string, string>): HomePageContent {
  const raw = blocks['home_page_config'];
  if (!raw) return DEFAULT_HOME_PAGE_CONTENT;

  try {
    const parsed = JSON.parse(raw) as CmsValue;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return DEFAULT_HOME_PAGE_CONTENT;
    return {
      seo: mergeObject(DEFAULT_HOME_PAGE_CONTENT.seo, parsed['seo']),
      hero: { ...mergeObject(DEFAULT_HOME_PAGE_CONTENT.hero, parsed['hero']), trustPoints: mergeStrings(DEFAULT_HOME_PAGE_CONTENT.hero.trustPoints, objectValue(parsed['hero'])?.['trustPoints']) },
      features: { ...mergeObject(DEFAULT_HOME_PAGE_CONTENT.features, parsed['features']), items: mergeFeatures(objectValue(parsed['features'])?.['items']) },
      approach: { ...mergeObject(DEFAULT_HOME_PAGE_CONTENT.approach, parsed['approach']), items: mergeApproach(objectValue(parsed['approach'])?.['items']) },
      pricing: mergeObject(DEFAULT_HOME_PAGE_CONTENT.pricing, parsed['pricing']),
      blog: mergeObject(DEFAULT_HOME_PAGE_CONTENT.blog, parsed['blog']),
      newsletter: { ...mergeObject(DEFAULT_HOME_PAGE_CONTENT.newsletter, parsed['newsletter']), benefits: mergeStrings(DEFAULT_HOME_PAGE_CONTENT.newsletter.benefits, objectValue(parsed['newsletter'])?.['benefits']) },
      faq: mergeObject(DEFAULT_HOME_PAGE_CONTENT.faq, parsed['faq']),
      cta: mergeObject(DEFAULT_HOME_PAGE_CONTENT.cta, parsed['cta']),
      visibility: mergeVisibility(parsed['visibility'])
    };
  } catch {
    return DEFAULT_HOME_PAGE_CONTENT;
  }
}

function objectValue(value: unknown): CmsValue | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as CmsValue : null;
}

function mergeObject<T extends object>(defaults: T, value: unknown): T {
  const source = objectValue(value);
  if (!source) return { ...defaults };
  return Object.fromEntries(Object.entries(defaults).map(([key, fallback]) => [key, typeof source[key] === 'string' ? source[key].trim() || fallback : fallback])) as T;
}

function mergeStrings(defaults: string[], value: unknown): string[] {
  if (!Array.isArray(value)) return [...defaults];
  const items = value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0).map(item => item.trim()).slice(0, 8);
  return items.length ? items : [...defaults];
}

function mergeFeatures(value: unknown): HomeFeatureContent[] {
  if (!Array.isArray(value)) return DEFAULT_HOME_PAGE_CONTENT.features.items.map(item => ({ ...item }));
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    icon: stringValue(item['icon'], 'auto_awesome'), title: stringValue(item['title']), description: stringValue(item['description'])
  })).filter(item => item.title && item.description).slice(0, 8);
  return items.length ? items : DEFAULT_HOME_PAGE_CONTENT.features.items.map(item => ({ ...item }));
}

function mergeApproach(value: unknown): HomeApproachContent[] {
  if (!Array.isArray(value)) return DEFAULT_HOME_PAGE_CONTENT.approach.items.map(item => ({ ...item }));
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    title: stringValue(item['title']), role: stringValue(item['role']), description: stringValue(item['description'])
  })).filter(item => item.title && item.description).slice(0, 6);
  return items.length ? items : DEFAULT_HOME_PAGE_CONTENT.approach.items.map(item => ({ ...item }));
}

function mergeVisibility(value: unknown): HomePageContent['visibility'] {
  const source = objectValue(value);
  return Object.fromEntries(Object.entries(DEFAULT_HOME_PAGE_CONTENT.visibility).map(([key, fallback]) => [key, typeof source?.[key] === 'boolean' ? source[key] : fallback])) as HomePageContent['visibility'];
}

function stringValue(value: unknown, fallback = ''): string {
  return typeof value === 'string' && value.trim() ? value.trim() : fallback;
}
