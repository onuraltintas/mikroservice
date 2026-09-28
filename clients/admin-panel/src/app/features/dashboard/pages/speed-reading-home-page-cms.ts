export interface HomePageDraft {
  seoTitle: string;
  seoDescription: string;
  seoKeywords: string;
  seoOgImage: string;
  heroTitle: string;
  heroSubtitle: string;
  primaryActionLabel: string;
  secondaryActionLabel: string;
  featuresTitle: string;
  featuresSubtitle: string;
  approachTitle: string;
  approachSubtitle: string;
  pricingTitle: string;
  pricingSubtitle: string;
  blogTitle: string;
  blogSubtitle: string;
  newsletterTitle: string;
  newsletterSubtitle: string;
  faqTitle: string;
  faqSubtitle: string;
  testimonialsTitle: string;
  testimonialsSubtitle: string;
  ctaTitle: string;
  ctaSubtitle: string;
  ctaActionLabel: string;
  ctaSmallText: string;
  featuresVisible: boolean;
  approachVisible: boolean;
  statsVisible: boolean;
  pricingVisible: boolean;
  blogVisible: boolean;
  newsletterVisible: boolean;
  faqVisible: boolean;
  testimonialsVisible: boolean;
  ctaVisible: boolean;
}

export interface HomeFeatureDraft {
  icon: string;
  title: string;
  description: string;
}

export interface HomeApproachDraft {
  title: string;
  role: string;
  description: string;
}

export interface HomeFaqDraft {
  question: string;
  answer: string;
  category: string;
}

export interface HomeTestimonialDraft {
  name: string;
  role: string;
  rating: number;
  text: string;
}

export interface HomeCardsDraft {
  trustPoints: string[];
  features: HomeFeatureDraft[];
  approach: HomeApproachDraft[];
  benefits: string[];
  faqItems: HomeFaqDraft[];
  testimonials: HomeTestimonialDraft[];
}

export interface HomePageCmsConfig {
  seo: { title: string; description: string; keywords: string; ogImage: string };
  hero: { title: string; subtitle: string; primaryActionLabel: string; secondaryActionLabel: string; trustPoints: string[] };
  features: { title: string; subtitle: string; items: HomeFeatureDraft[] };
  approach: { title: string; subtitle: string; items: HomeApproachDraft[] };
  pricing: { title: string; subtitle: string };
  blog: { title: string; subtitle: string };
  newsletter: { title: string; subtitle: string; benefits: string[] };
  faq: { title: string; subtitle: string; items: HomeFaqDraft[] };
  testimonials: { title: string; subtitle: string; items: HomeTestimonialDraft[] };
  cta: { title: string; subtitle: string; actionLabel: string; smallText: string };
  visibility: {
    features: boolean; approach: boolean; stats: boolean; pricing: boolean; blog: boolean;
    newsletter: boolean; faq: boolean; testimonials: boolean; cta: boolean;
  };
}

export const DEFAULT_HOME_PAGE_DRAFT: HomePageDraft = {
  seoTitle: 'Master Hızlı Okuma | Okuma Hızı ve Anlama Takibi',
  seoDescription: 'Başlangıç ölçümünüzü yapın; okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin.',
  seoKeywords: 'hızlı okuma, okuduğunu anlama, okuma egzersizleri, kişisel öğrenme planı',
  seoOgImage: '',
  heroTitle: 'Hız ve anlamayı birlikte takip edin',
  heroSubtitle: 'Başlangıç ölçümünüzü yapın; okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin.',
  primaryActionLabel: 'Seviyeni belirle',
  secondaryActionLabel: 'Nasıl çalışır?',
  featuresTitle: 'Çalışma akışında neler var?',
  featuresSubtitle: 'Odak, okuma akıcılığı ve anlama çalışmalarını aynı öğrenme alanında keşfedin.',
  approachTitle: 'Ölçerek ilerleyen bir çalışma düzeni',
  approachSubtitle: 'Tek bir hız hedefine bağlanmadan, okuma süresi ve anlama sonuçlarını birlikte izleyin.',
  pricingTitle: 'Size uygun erişimi seçin',
  pricingSubtitle: 'Bireysel veya kurumsal erişim seçeneklerini inceleyin.',
  blogTitle: 'Kaynaklar ve çalışma ipuçları',
  blogSubtitle: 'Okuma, anlama ve düzenli çalışma üzerine içerikleri keşfedin.',
  newsletterTitle: 'Çalışma ipuçları e-postanıza gelsin',
  newsletterSubtitle: 'Yeni içeriklerden ve yararlı çalışma önerilerinden haberdar olun.',
  faqTitle: 'Sık sorulan sorular',
  faqSubtitle: 'Platform ve çalışma düzeni hakkında kısa yanıtlar.',
  testimonialsTitle: 'Katılımcı deneyimleri',
  testimonialsSubtitle: 'Platform hakkındaki yayımlanmış geri bildirimler.',
  ctaTitle: 'Okuma çalışma düzeninizi keşfedin',
  ctaSubtitle: 'Başlangıç ölçümünüzü yapın ve okuma sonuçlarınızı ayrı ayrı inceleyin.',
  ctaActionLabel: 'Başla',
  ctaSmallText: 'Sonuçlar metin türüne, zorluğuna ve çalışma koşullarına göre değişebilir.',
  featuresVisible: true,
  approachVisible: true,
  statsVisible: true,
  pricingVisible: true,
  blogVisible: true,
  newsletterVisible: true,
  faqVisible: true,
  testimonialsVisible: false,
  ctaVisible: true
};

export const DEFAULT_HOME_PAGE_CARDS: HomeCardsDraft = {
  trustPoints: ['Başlangıç ölçümü', 'Hız ve anlama birlikte', 'Kişisel çalışma akışı'],
  features: [
    { icon: 'speed', title: 'Akıcılık çalışmaları', description: 'Metin takibi ve kelime gruplama alıştırmalarını deneyin.' },
    { icon: 'quiz', title: 'Anlama kontrolü', description: 'Okuma sonrasında anlama sorularını ve yanıtlarınızı gözden geçirin.' },
    { icon: 'route', title: 'Çalışma akışı', description: 'Çalışma adımlarınızı ve tamamladığınız içerikleri takip edin.' },
    { icon: 'insights', title: 'İlerleme görünümü', description: 'Okuma hızı ve anlama sonuçlarınızı ayrı ayrı inceleyin.' }
  ],
  approach: [
    { title: 'Başlangıcı görün', role: 'Ölçüm', description: 'Başlangıç ölçümünde okuma sürenizi ve anlama yanıtlarınızı görün.' },
    { title: 'İki sonucu birlikte izleyin', role: 'Hız + anlama', description: 'Her çalışmada okuma hızı ve anlama sonuçlarını birlikte değerlendirin.' },
    { title: 'Çalışma geçmişinizi inceleyin', role: 'İzleme', description: 'Önceki sonuçlarınızı gözden geçirip sonraki çalışma alanınızı seçin.' }
  ],
  benefits: ['Yeni çalışma önerileri', 'Güncel içerikler', 'İstediğiniz zaman abonelikten çıkma özgürlüğü'],
  faqItems: [],
  testimonials: []
};

export interface HomePageBlockInput {
  key: string;
  value: string;
}

export function hydrateHomePageDraft(blocks: readonly HomePageBlockInput[]): { draft: HomePageDraft; cards: HomeCardsDraft } {
  const values = new Map(blocks.map(block => [block.key, block.value]));
  const config = parseObject(values.get('home_page_config'));
  const section = (name: string) => objectValue(config?.[name]);
  const seo = section('seo');
  const hero = section('hero');
  const features = section('features');
  const approach = section('approach');
  const pricing = section('pricing');
  const blog = section('blog');
  const newsletter = section('newsletter');
  const faq = section('faq');
  const testimonials = section('testimonials');
  const cta = section('cta');
  const visibility = section('visibility');
  const text = (source: Record<string, unknown> | null, key: string, legacyKey: string, fallback: string) =>
    readString(source?.[key]) || readString(values.get(legacyKey)) || fallback;
  const readList = <T>(configValue: unknown, legacyKey: string, normalize: (value: unknown) => T | null, fallback: T[]): T[] => {
    if (Array.isArray(configValue)) return normalizeList(configValue, normalize);
    const legacyList = parseArray(values.get(legacyKey));
    return legacyList ? normalizeList(legacyList, normalize) : [...fallback];
  };
  const cards: HomeCardsDraft = {
    trustPoints: readStringList(hero?.['trustPoints'], DEFAULT_HOME_PAGE_CARDS.trustPoints),
    features: readList(features?.['items'], 'features_list', normalizeFeature, DEFAULT_HOME_PAGE_CARDS.features),
    approach: readList(approach?.['items'], '', normalizeApproach, DEFAULT_HOME_PAGE_CARDS.approach),
    benefits: readStringList(newsletter?.['benefits'], DEFAULT_HOME_PAGE_CARDS.benefits),
    faqItems: readList(faq?.['items'], 'faq_items', normalizeFaq, []).slice(0, 8),
    testimonials: readList(testimonials?.['items'], 'testimonials_list', normalizeTestimonial, []).slice(0, 8)
  };

  if (!faq?.['items'] && !values.has('faq_items')) {
    cards.faqItems = readList(faq?.['items'], 'faq_list', normalizeFaq, []).slice(0, 8);
  }

  const draft: HomePageDraft = {
    ...DEFAULT_HOME_PAGE_DRAFT,
    seoTitle: text(seo, 'title', '', DEFAULT_HOME_PAGE_DRAFT.seoTitle),
    seoDescription: text(seo, 'description', '', DEFAULT_HOME_PAGE_DRAFT.seoDescription),
    seoKeywords: text(seo, 'keywords', '', DEFAULT_HOME_PAGE_DRAFT.seoKeywords),
    seoOgImage: text(seo, 'ogImage', '', DEFAULT_HOME_PAGE_DRAFT.seoOgImage),
    heroTitle: text(hero, 'title', 'hero_title', DEFAULT_HOME_PAGE_DRAFT.heroTitle),
    heroSubtitle: text(hero, 'subtitle', 'hero_subtitle', DEFAULT_HOME_PAGE_DRAFT.heroSubtitle),
    primaryActionLabel: text(hero, 'primaryActionLabel', 'hero_cta_text', DEFAULT_HOME_PAGE_DRAFT.primaryActionLabel),
    secondaryActionLabel: text(hero, 'secondaryActionLabel', '', DEFAULT_HOME_PAGE_DRAFT.secondaryActionLabel),
    featuresTitle: text(features, 'title', 'features_title', DEFAULT_HOME_PAGE_DRAFT.featuresTitle),
    featuresSubtitle: text(features, 'subtitle', 'features_subtitle', DEFAULT_HOME_PAGE_DRAFT.featuresSubtitle),
    approachTitle: text(approach, 'title', '', DEFAULT_HOME_PAGE_DRAFT.approachTitle),
    approachSubtitle: text(approach, 'subtitle', '', DEFAULT_HOME_PAGE_DRAFT.approachSubtitle),
    pricingTitle: text(pricing, 'title', 'pricing_title', DEFAULT_HOME_PAGE_DRAFT.pricingTitle),
    pricingSubtitle: text(pricing, 'subtitle', 'pricing_subtitle', DEFAULT_HOME_PAGE_DRAFT.pricingSubtitle),
    blogTitle: text(blog, 'title', '', DEFAULT_HOME_PAGE_DRAFT.blogTitle),
    blogSubtitle: text(blog, 'subtitle', '', DEFAULT_HOME_PAGE_DRAFT.blogSubtitle),
    newsletterTitle: text(newsletter, 'title', '', DEFAULT_HOME_PAGE_DRAFT.newsletterTitle),
    newsletterSubtitle: text(newsletter, 'subtitle', '', DEFAULT_HOME_PAGE_DRAFT.newsletterSubtitle),
    faqTitle: text(faq, 'title', 'faq_title', DEFAULT_HOME_PAGE_DRAFT.faqTitle),
    faqSubtitle: text(faq, 'subtitle', 'faq_subtitle', DEFAULT_HOME_PAGE_DRAFT.faqSubtitle),
    testimonialsTitle: text(testimonials, 'title', 'testimonials_title', DEFAULT_HOME_PAGE_DRAFT.testimonialsTitle),
    testimonialsSubtitle: text(testimonials, 'subtitle', 'testimonials_subtitle', DEFAULT_HOME_PAGE_DRAFT.testimonialsSubtitle),
    ctaTitle: text(cta, 'title', 'cta_title', DEFAULT_HOME_PAGE_DRAFT.ctaTitle),
    ctaSubtitle: text(cta, 'subtitle', 'cta_description', DEFAULT_HOME_PAGE_DRAFT.ctaSubtitle),
    ctaActionLabel: text(cta, 'actionLabel', 'cta_button_text', DEFAULT_HOME_PAGE_DRAFT.ctaActionLabel),
    ctaSmallText: text(cta, 'smallText', '', DEFAULT_HOME_PAGE_DRAFT.ctaSmallText),
    featuresVisible: bool(visibility?.['features'], DEFAULT_HOME_PAGE_DRAFT.featuresVisible),
    approachVisible: bool(visibility?.['approach'], DEFAULT_HOME_PAGE_DRAFT.approachVisible),
    statsVisible: bool(visibility?.['stats'], DEFAULT_HOME_PAGE_DRAFT.statsVisible),
    pricingVisible: bool(visibility?.['pricing'], DEFAULT_HOME_PAGE_DRAFT.pricingVisible),
    blogVisible: bool(visibility?.['blog'], DEFAULT_HOME_PAGE_DRAFT.blogVisible),
    newsletterVisible: bool(visibility?.['newsletter'], DEFAULT_HOME_PAGE_DRAFT.newsletterVisible),
    faqVisible: bool(visibility?.['faq'], DEFAULT_HOME_PAGE_DRAFT.faqVisible),
    testimonialsVisible: bool(visibility?.['testimonials'], cards.testimonials.length > 0),
    ctaVisible: bool(visibility?.['cta'], DEFAULT_HOME_PAGE_DRAFT.ctaVisible)
  };

  return { draft, cards };
}

export function buildHomePageConfig(draft: HomePageDraft, cards: HomeCardsDraft): HomePageCmsConfig {
  return {
    seo: { title: draft.seoTitle.trim(), description: draft.seoDescription.trim(), keywords: draft.seoKeywords.trim(), ogImage: draft.seoOgImage.trim() },
    hero: {
      title: draft.heroTitle.trim(), subtitle: draft.heroSubtitle.trim(), primaryActionLabel: draft.primaryActionLabel.trim(),
      secondaryActionLabel: draft.secondaryActionLabel.trim(), trustPoints: cleanStrings(cards.trustPoints)
    },
    features: {
      title: draft.featuresTitle.trim(), subtitle: draft.featuresSubtitle.trim(),
      items: cards.features.map(item => ({ icon: item.icon.trim(), title: item.title.trim(), description: item.description.trim() })).filter(item => item.title && item.description)
    },
    approach: {
      title: draft.approachTitle.trim(), subtitle: draft.approachSubtitle.trim(),
      items: cards.approach.map(item => ({ title: item.title.trim(), role: item.role.trim(), description: item.description.trim() })).filter(item => item.title && item.description)
    },
    pricing: { title: draft.pricingTitle.trim(), subtitle: draft.pricingSubtitle.trim() },
    blog: { title: draft.blogTitle.trim(), subtitle: draft.blogSubtitle.trim() },
    newsletter: { title: draft.newsletterTitle.trim(), subtitle: draft.newsletterSubtitle.trim(), benefits: cleanStrings(cards.benefits) },
    faq: {
      title: draft.faqTitle.trim(), subtitle: draft.faqSubtitle.trim(),
      items: cards.faqItems.map(item => ({ question: item.question.trim(), answer: item.answer.trim(), category: item.category.trim() })).filter(item => item.question && item.answer).slice(0, 8)
    },
    testimonials: {
      title: draft.testimonialsTitle.trim(), subtitle: draft.testimonialsSubtitle.trim(),
      items: cards.testimonials.map(item => ({ name: item.name.trim(), role: item.role.trim(), rating: validRating(item.rating), text: item.text.trim() })).filter(item => item.name && item.text).slice(0, 8)
    },
    cta: { title: draft.ctaTitle.trim(), subtitle: draft.ctaSubtitle.trim(), actionLabel: draft.ctaActionLabel.trim(), smallText: draft.ctaSmallText.trim() },
    visibility: {
      features: draft.featuresVisible, approach: draft.approachVisible, stats: draft.statsVisible, pricing: draft.pricingVisible,
      blog: draft.blogVisible, newsletter: draft.newsletterVisible, faq: draft.faqVisible,
      testimonials: draft.testimonialsVisible, cta: draft.ctaVisible
    }
  };
}

function parseObject(value: string | undefined): Record<string, unknown> | null {
  if (!value) return null;
  try {
    const parsed: unknown = JSON.parse(value);
    return objectValue(parsed);
  } catch {
    return null;
  }
}

function parseArray(value: string | undefined): unknown[] | null {
  if (!value) return null;
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function objectValue(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;
}

function readString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function bool(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}

function readStringList(value: unknown, fallback: string[]): string[] {
  return Array.isArray(value) ? cleanStrings(value.filter((item): item is string => typeof item === 'string')) : [...fallback];
}

function cleanStrings(values: readonly string[]): string[] {
  return values.map(value => value.trim()).filter(Boolean);
}

function normalizeList<T>(values: unknown[], normalize: (value: unknown) => T | null): T[] {
  return values.map(normalize).filter((item): item is T => item !== null);
}

function normalizeFeature(value: unknown): HomeFeatureDraft | null {
  const item = objectValue(value);
  if (!item) return null;
  const title = readString(item['title']);
  const description = readString(item['description']);
  return title && description ? { icon: readString(item['icon']) || 'auto_awesome', title, description } : null;
}

function normalizeApproach(value: unknown): HomeApproachDraft | null {
  const item = objectValue(value);
  if (!item) return null;
  const title = readString(item['title']);
  const description = readString(item['description']);
  return title && description ? { title, role: readString(item['role']), description } : null;
}

function normalizeFaq(value: unknown): HomeFaqDraft | null {
  const item = objectValue(value);
  if (!item) return null;
  const question = readString(item['question']);
  const answer = readString(item['answer']);
  return question && answer ? { question, answer, category: readString(item['category']) } : null;
}

function normalizeTestimonial(value: unknown): HomeTestimonialDraft | null {
  const item = objectValue(value);
  if (!item) return null;
  const name = readString(item['name']);
  const text = readString(item['text']);
  const rating = validRating(item['rating']);
  return name && text ? { name, role: readString(item['role']), rating, text } : null;
}

function validRating(value: unknown): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1 && value <= 5 ? value : 0;
}
