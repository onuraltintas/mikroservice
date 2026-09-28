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
  primaryActionUrl: string;
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

export interface HomeFaqItem {
  question: string;
  answer: string;
  category: string;
}

export interface HomeTestimonialItem {
  name: string;
  role: string;
  rating: number;
  text: string;
}

export interface HomeSectionHeading {
  title: string;
  subtitle: string;
  actionLabel?: string;
  actionUrl?: string;
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
  faq: HomeSectionHeading & { items: HomeFaqItem[] };
  testimonials: HomeSectionHeading & { items: HomeTestimonialItem[] };
  cta: HomeSectionHeading;
  visibility: Record<'features' | 'approach' | 'stats' | 'pricing' | 'blog' | 'newsletter' | 'faq' | 'testimonials' | 'cta', boolean>;
}

export const DEFAULT_HOME_PAGE_CONTENT: HomePageContent = {
  seo: {
    title: 'Master Hızlı Okuma | Okuma Hızı ve Anlama Takibi',
    description: 'Başlangıç ölçümünüzü yapın; okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin.',
    keywords: 'hızlı okuma, okuduğunu anlama, okuma egzersizleri, kişisel öğrenme planı',
    ogImage: ''
  },
  hero: {
    title: 'Hız ve anlamayı birlikte takip edin',
    subtitle: 'Başlangıç ölçümünüzü yapın; okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin.',
    primaryActionLabel: 'Seviyeni belirle',
    primaryActionUrl: '/auth/register',
    secondaryActionLabel: 'Nasıl çalışır?',
    trustPoints: ['Başlangıç ölçümü', 'Hız ve anlama birlikte', 'Kişisel çalışma akışı']
  },
  features: {
    title: 'Çalışma akışında neler var?',
    subtitle: 'Odak, okuma akıcılığı ve anlama çalışmalarını aynı öğrenme alanında keşfedin.',
    items: [
      { icon: 'speed', title: 'Akıcılık çalışmaları', description: 'Metin takibi ve kelime gruplama alıştırmalarını deneyin.' },
      { icon: 'quiz', title: 'Anlama kontrolü', description: 'Okuma sonrasında anlama sorularını ve yanıtlarınızı gözden geçirin.' },
      { icon: 'route', title: 'Çalışma akışı', description: 'Çalışma adımlarınızı ve tamamladığınız içerikleri takip edin.' },
      { icon: 'insights', title: 'İlerleme görünümü', description: 'Okuma hızı ve anlama sonuçlarınızı ayrı ayrı inceleyin.' }
    ]
  },
  approach: {
    title: 'Ölçerek ilerleyen bir çalışma düzeni',
    subtitle: 'Tek bir hız hedefine bağlanmadan, okuma süresi ve anlama sonuçlarını birlikte izleyin.',
    items: [
      { title: 'Başlangıcı görün', role: 'Ölçüm', description: 'Başlangıç ölçümünde okuma sürenizi ve anlama yanıtlarınızı görün.' },
      { title: 'İki sonucu birlikte izleyin', role: 'Hız + anlama', description: 'Her çalışmada okuma hızı ve anlama sonuçlarını birlikte değerlendirin.' },
      { title: 'Çalışma geçmişinizi inceleyin', role: 'İzleme', description: 'Önceki sonuçlarınızı gözden geçirip sonraki çalışma alanınızı seçin.' }
    ]
  },
  pricing: { title: 'Size uygun erişimi seçin', subtitle: 'Bireysel veya kurumsal erişim seçeneklerini inceleyin.' },
  blog: { title: 'Kaynaklar ve çalışma ipuçları', subtitle: 'Okuma, anlama ve düzenli çalışma üzerine içerikleri keşfedin.', actionLabel: 'Tüm yazıları gör' },
  newsletter: {
    title: 'Çalışma ipuçları e-postanıza gelsin',
    subtitle: 'Yeni içeriklerden ve yararlı çalışma önerilerinden haberdar olun.',
    benefits: ['Yeni çalışma önerileri', 'Güncel içerikler', 'İstediğiniz zaman abonelikten çıkma özgürlüğü']
  },
  faq: { title: 'Sık sorulan sorular', subtitle: 'Platform ve çalışma düzeni hakkında kısa yanıtlar.', actionLabel: 'Tüm soruları gör', items: [] },
  testimonials: { title: 'Katılımcı deneyimleri', subtitle: 'Platform hakkındaki yayımlanmış geri bildirimler.', items: [] },
  cta: {
    title: 'Okuma çalışma düzeninizi keşfedin',
    subtitle: 'Başlangıç ölçümünüzü yapın ve okuma sonuçlarınızı ayrı ayrı inceleyin.',
    actionLabel: 'Başla',
    smallText: 'Sonuçlar metin türüne, zorluğuna ve çalışma koşullarına göre değişebilir.'
  },
  visibility: { features: true, approach: true, stats: true, pricing: true, blog: true, newsletter: true, faq: true, testimonials: false, cta: true }
};

type CmsValue = Record<string, unknown>;

export function parseHomePageContent(blocks: Record<string, string>): HomePageContent {
  const legacyContent = parseLegacyHomePageContent(blocks);
  const raw = blocks['home_page_config'];
  if (!raw) return legacyContent;

  try {
    const parsed = JSON.parse(raw) as CmsValue;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return legacyContent;
    const parsedHero = mergeObject(legacyContent.hero, parsed['hero']);
    const parsedCta = mergeObject(legacyContent.cta, parsed['cta']);
    return {
      seo: mergeObject(legacyContent.seo, parsed['seo']),
      hero: {
        ...parsedHero,
        primaryActionUrl: internalRoute(parsedHero.primaryActionUrl, legacyContent.hero.primaryActionUrl),
        trustPoints: mergeStrings(legacyContent.hero.trustPoints, objectValue(parsed['hero'])?.['trustPoints'])
      },
      features: { ...mergeObject(legacyContent.features, parsed['features']), items: mergeFeatures(objectValue(parsed['features'])?.['items'], legacyContent.features.items) },
      approach: { ...mergeObject(legacyContent.approach, parsed['approach']), items: mergeApproach(objectValue(parsed['approach'])?.['items'], legacyContent.approach.items) },
      pricing: mergeObject(legacyContent.pricing, parsed['pricing']),
      blog: mergeObject(legacyContent.blog, parsed['blog']),
      newsletter: { ...mergeObject(legacyContent.newsletter, parsed['newsletter']), benefits: mergeStrings(legacyContent.newsletter.benefits, objectValue(parsed['newsletter'])?.['benefits']) },
      faq: { ...mergeObject(legacyContent.faq, parsed['faq']), items: mergeFaqItems(objectValue(parsed['faq'])?.['items'], legacyContent.faq.items) },
      testimonials: { ...mergeObject(legacyContent.testimonials, parsed['testimonials']), items: mergeTestimonials(objectValue(parsed['testimonials'])?.['items'], legacyContent.testimonials.items) },
      cta: { ...parsedCta, actionUrl: internalRoute(parsedCta.actionUrl, '/auth/register') },
      visibility: mergeVisibility(parsed['visibility'], legacyContent.visibility)
    };
  } catch {
    return legacyContent;
  }
}

function parseLegacyHomePageContent(blocks: Record<string, string>): HomePageContent {
  const defaults = DEFAULT_HOME_PAGE_CONTENT;
  const hero = { ...defaults.hero };
  hero.title = stringValue(blocks['hero_title'], hero.title);
  hero.subtitle = stringValue(blocks['hero_subtitle'], hero.subtitle);
  hero.primaryActionLabel = stringValue(blocks['hero_cta_text'], hero.primaryActionLabel);
  hero.primaryActionUrl = internalRoute(blocks['hero_cta_link'], hero.primaryActionUrl);

  const features = {
    ...defaults.features,
    title: stringValue(blocks['features_title'], defaults.features.title),
    subtitle: stringValue(blocks['features_subtitle'], defaults.features.subtitle),
    items: mergeFeatures(parseJsonValue(blocks['features_list']), defaults.features.items)
  };
  const faqItems = mergeFaqItems(parseJsonValue(blocks['faq_items'] ?? blocks['faq_list']), defaults.faq.items);
  const testimonialItems = mergeTestimonials(parseJsonValue(blocks['testimonials_list']), defaults.testimonials.items);

  return {
    ...defaults,
    hero,
    features,
    pricing: {
      ...defaults.pricing,
      title: stringValue(blocks['pricing_title'], defaults.pricing.title),
      subtitle: stringValue(blocks['pricing_subtitle'], defaults.pricing.subtitle)
    },
    faq: {
      ...defaults.faq,
      title: stringValue(blocks['faq_title'], defaults.faq.title),
      subtitle: stringValue(blocks['faq_subtitle'], defaults.faq.subtitle),
      items: faqItems
    },
    testimonials: {
      ...defaults.testimonials,
      title: stringValue(blocks['testimonials_title'], defaults.testimonials.title),
      subtitle: stringValue(blocks['testimonials_subtitle'], defaults.testimonials.subtitle),
      items: testimonialItems
    },
    cta: {
      ...defaults.cta,
      title: stringValue(blocks['cta_title'], defaults.cta.title),
      subtitle: stringValue(blocks['cta_description'], defaults.cta.subtitle),
      actionLabel: stringValue(blocks['cta_button_text'], defaults.cta.actionLabel ?? 'Başla'),
      actionUrl: internalRoute(blocks['cta_button_link'], '/auth/register')
    },
    visibility: { ...defaults.visibility, testimonials: testimonialItems.length > 0 }
  };
}

function objectValue(value: unknown): CmsValue | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as CmsValue : null;
}

function mergeObject<T extends object>(defaults: T, value: unknown): T {
  const source = objectValue(value);
  if (!source) return { ...defaults };
  return Object.fromEntries(Object.entries(defaults).map(([key, fallback]) => {
    const candidate = typeof source[key] === 'string' ? source[key].trim() : '';
    return [key, isSafePublicCopy(candidate) ? candidate || fallback : fallback];
  })) as T;
}

function mergeStrings(defaults: string[], value: unknown): string[] {
  if (!Array.isArray(value)) return [...defaults];
  const items = value
    .filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
    .map(item => item.trim())
    .filter(isSafePublicCopy)
    .slice(0, 8);
  return items.length ? items : [...defaults];
}

function mergeFeatures(value: unknown, defaults = DEFAULT_HOME_PAGE_CONTENT.features.items): HomeFeatureContent[] {
  if (!Array.isArray(value)) return defaults.map(item => ({ ...item }));
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    icon: safeIcon(item['icon']), title: stringValue(item['title']), description: stringValue(item['description'])
  })).filter(item => item.title && item.description && isSafePublicCopy(item.title) && isSafePublicCopy(item.description)).slice(0, 8);
  return items.length ? items : defaults.map(item => ({ ...item }));
}

function mergeApproach(value: unknown, defaults = DEFAULT_HOME_PAGE_CONTENT.approach.items): HomeApproachContent[] {
  if (!Array.isArray(value)) return defaults.map(item => ({ ...item }));
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    title: stringValue(item['title']), role: stringValue(item['role']), description: stringValue(item['description'])
  })).filter(item => item.title && item.description && isSafePublicCopy(item.title) && isSafePublicCopy(item.description)).slice(0, 6);
  return items.length ? items : defaults.map(item => ({ ...item }));
}

function mergeFaqItems(value: unknown, defaults = DEFAULT_HOME_PAGE_CONTENT.faq.items): HomeFaqItem[] {
  if (!Array.isArray(value)) return defaults.map(item => ({ ...item }));
  if (value.length === 0) return [];
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    question: stringValue(item['question']), answer: stringValue(item['answer']), category: stringValue(item['category'])
  })).filter(item => item.question && item.answer).slice(0, 8);
  return items.length ? items : defaults.map(item => ({ ...item }));
}

function mergeTestimonials(value: unknown, defaults = DEFAULT_HOME_PAGE_CONTENT.testimonials.items): HomeTestimonialItem[] {
  if (!Array.isArray(value)) return defaults.map(item => ({ ...item }));
  if (value.length === 0) return [];
  const items = value.map(objectValue).filter((item): item is CmsValue => item !== null).map(item => ({
    name: stringValue(item['name']), role: stringValue(item['role']),
    rating: typeof item['rating'] === 'number' && Number.isFinite(item['rating']) && item['rating'] >= 1 && item['rating'] <= 5 ? Math.round(item['rating']) : 0,
    text: stringValue(item['text'])
  })).filter(item => item.name && item.text).slice(0, 8);
  return items.length ? items : defaults.map(item => ({ ...item }));
}

function mergeVisibility(value: unknown, defaults = DEFAULT_HOME_PAGE_CONTENT.visibility): HomePageContent['visibility'] {
  const source = objectValue(value);
  return Object.fromEntries(Object.entries(defaults).map(([key, fallback]) => [key, typeof source?.[key] === 'boolean' ? source[key] : fallback])) as HomePageContent['visibility'];
}

function stringValue(value: unknown, fallback = ''): string {
  const candidate = typeof value === 'string' ? value.trim() : '';
  return isSafePublicCopy(candidate) ? candidate || fallback : fallback;
}

function parseJsonValue(value: string | undefined): unknown {
  if (!value) return undefined;
  try { return JSON.parse(value) as unknown; } catch { return undefined; }
}

function safeIcon(value: unknown): string {
  const icon = typeof value === 'string' ? value.trim() : '';
  return /^[a-z0-9_]+$/i.test(icon) ? icon : 'auto_awesome';
}

function internalRoute(value: string | undefined, fallback: string): string {
  const candidate = value?.trim() ?? '';
  if (!candidate.startsWith('/') || candidate.startsWith('//') || candidate.includes('\\')) return fallback;
  try {
    const parsed = new URL(candidate, 'https://internal.invalid');
    return parsed.origin === 'https://internal.invalid' ? candidate : fallback;
  } catch {
    return fallback;
  }
}

/** Public marketing copy must not publish unverified outcome guarantees or speed claims. */
function isSafePublicCopy(value: string): boolean {
  if (!value) return true;
  return ![
    /\b\d+\s*kat(?:la\w*|lan\w*|ına|ını|ı|a)?\b/i,
    /\bikiye\s+kat(?:la\w*|lan\w*)?\b/i,
    /\b\d{2,4}\s*\+?\s*wpm\b/i,
    /(?:\b\d{1,3}\s*%|%\s*\d{1,3}\b)/i,
    /(?:\byüzde\s+\d{1,3}\b|\b\d{1,3}\s+yüzde\b)/i,
    /\bkesin\w*\b/i,
    /\bgaranti\w*\b/i,
    /bilimsel\s+yöntemlerle/i,
    /akademik\s+başarı\w*\s+(?:yükselt|artır)/i
  ].some(pattern => pattern.test(value));
}
