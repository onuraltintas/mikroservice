import { DEFAULT_HOME_PAGE_CONTENT, parseHomePageContent } from './home-page-content';

describe('parseHomePageContent', () => {
  it('uses the published CMS configuration for the home page', () => {
    const content = parseHomePageContent({
      home_page_config: JSON.stringify({
        seo: {
          title: 'Ölçerek ilerleyin | Master Hızlı Okuma',
          description: 'Kişisel çalışma planınızı görün.'
        },
        hero: {
          title: 'Kişisel çalışma yolunuz',
          subtitle: 'Hız ve anlama birlikte izlenir.',
          primaryActionLabel: 'Programa başla'
        },
        visibility: { newsletter: false }
      })
    });

    expect(content.seo.title).toBe('Ölçerek ilerleyin | Master Hızlı Okuma');
    expect(content.hero.title).toBe('Kişisel çalışma yolunuz');
    expect(content.hero.primaryActionLabel).toBe('Programa başla');
    expect(content.visibility.newsletter).toBeFalse();
    expect(content.visibility.features).toBeTrue();
  });

  it('keeps safe defaults when the CMS payload is malformed', () => {
    const content = parseHomePageContent({ home_page_config: '{not-json' });

    expect(content.hero.title).toContain('Hız');
    expect(content.visibility.cta).toBeTrue();
  });

  it('describes observable reading measures without promising improvement', () => {
    expect(DEFAULT_HOME_PAGE_CONTENT.hero.title).toBe('Hız ve anlamayı birlikte takip edin');
    expect(DEFAULT_HOME_PAGE_CONTENT.hero.subtitle).toContain('okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin');
    expect(JSON.stringify(DEFAULT_HOME_PAGE_CONTENT)).not.toMatch(/size uygun çalışmalar|zorluk, sonuçlarınıza göre dengelenir/i);
  });

  it('maps legacy HomePage CMS fields to the current page contract', () => {
    const content = parseHomePageContent({
      hero_title: 'Okuma sürenizi takip edin',
      hero_subtitle: 'Okuma süresi ve anlama yanıtlarını birlikte görün.',
      hero_cta_text: 'Kayıt ol',
      hero_cta_link: '/auth/register',
      features_title: 'Çalışma alanları',
      features_subtitle: 'Farklı alıştırmaları keşfedin.',
      features_list: JSON.stringify([{ icon: 'menu_book', title: 'Metin takibi', description: 'Metin takibi alıştırmalarını deneyin.' }]),
      pricing_title: 'Erişim planları',
      pricing_subtitle: 'Etkin planları inceleyin.',
      pricing_plans: JSON.stringify([{ name: 'Eski paket kaydı' }]),
      faq_title: 'Merak edilenler',
      faq_items: JSON.stringify([{ question: 'Ölçüm nasıl yapılır?', answer: 'Okuma süresi ve yanıtlar birlikte gösterilir.', category: 'Ölçüm' }]),
      testimonials_title: 'Katılımcı deneyimleri',
      testimonials_subtitle: 'Yayımlanmış geri bildirimler.',
      testimonials_list: JSON.stringify([{ name: 'Ayşe', role: 'Öğrenci', avatar: '', rating: 5, text: 'Çalışma akışını takip etmek kolay.', date: '2026-09-01' }]),
      stats_users: '10000',
      stats_improvement: '185',
      stats_satisfaction: '96',
      stats_exercises: '250000',
      cta_title: 'Başlangıç ölçümünü yapın',
      cta_description: 'Okuma sonuçlarınızı gözden geçirin.',
      cta_button_text: 'Hemen başla',
      cta_button_link: '/auth/register'
    });
    const mapped = content as any;

    expect(mapped.hero.title).toBe('Okuma sürenizi takip edin');
    expect(mapped.hero.subtitle).toBe('Okuma süresi ve anlama yanıtlarını birlikte görün.');
    expect(mapped.hero.primaryActionLabel).toBe('Kayıt ol');
    expect(mapped.hero.primaryActionUrl).toBe('/auth/register');
    expect(mapped.features.items).toEqual([{ icon: 'menu_book', title: 'Metin takibi', description: 'Metin takibi alıştırmalarını deneyin.' }]);
    expect(mapped.pricing.title).toBe('Erişim planları');
    expect(mapped.faq.items).toEqual([{ question: 'Ölçüm nasıl yapılır?', answer: 'Okuma süresi ve yanıtlar birlikte gösterilir.', category: 'Ölçüm' }]);
    expect(mapped.testimonials.items.length).toBe(1);
    expect(mapped.cta.actionLabel).toBe('Hemen başla');
    expect(mapped.cta.actionUrl).toBe('/auth/register');
    expect(JSON.stringify(content)).not.toContain('10000');
    expect(JSON.stringify(content)).not.toContain('Eski paket kaydı');
  });

  it('prefers the new config but fills fields omitted from it with mapped legacy content', () => {
    const content = parseHomePageContent({
      hero_title: 'Eski başlık',
      features_title: 'Eski özellik başlığı',
      home_page_config: JSON.stringify({ hero: { title: 'Yeni başlık' } })
    });

    expect(content.hero.title).toBe('Yeni başlık');
    expect(content.features.title).toBe('Eski özellik başlığı');
  });

  it('respects explicitly empty FAQ and testimonial lists in the new configuration', () => {
    const content = parseHomePageContent({
      faq_items: JSON.stringify([{ question: 'Eski soru', answer: 'Eski yanıt', category: 'Genel' }]),
      testimonials_list: JSON.stringify([{ name: 'Ayşe', role: 'Öğrenci', rating: 5, text: 'Eski yorum.' }]),
      home_page_config: JSON.stringify({ faq: { items: [] }, testimonials: { items: [] }, visibility: { testimonials: false } })
    });

    expect(content.faq.items).toEqual([]);
    expect(content.testimonials.items).toEqual([]);
    expect(content.visibility.testimonials).toBeFalse();
  });

  it('rejects unsafe legacy copy and non-local action destinations', () => {
    const content = parseHomePageContent({
      hero_title: 'Okuma Hızınızı 3 Katına Çıkarın',
      hero_subtitle: 'Bilimsel yöntemlerle okuma hızınızı artırın ve akademik başarınızı yükseltin.',
      hero_cta_link: '//attacker.example/register',
      cta_button_link: 'javascript:alert(1)'
    });

    expect(content.hero.title).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.title);
    expect(content.hero.subtitle).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.subtitle);
    expect(content.hero.primaryActionUrl).toBe('/auth/register');
    expect(content.cta.actionUrl).toBe('/auth/register');
  });

  it('ignores malformed legacy lists and filters unsupported testimonial claims', () => {
    const content = parseHomePageContent({
      features_list: '{not-json',
      faq_items: JSON.stringify([{ question: 'Soru', answer: 'Yanıt', category: 'Genel' }]),
      testimonials_list: JSON.stringify([
        { name: 'Öğrenci', role: 'Katılımcı', rating: 5, text: 'Hızımı 3 kat artırdım.' },
        { name: 'Veli', role: 'Veli', rating: 5, text: 'Çalışma akışını takip etmek kolay.' }
      ])
    });

    expect(content.features.items).toEqual(DEFAULT_HOME_PAGE_CONTENT.features.items);
    expect(content.faq.items.length).toBe(1);
    expect(content.testimonials.items).toEqual([
      { name: 'Veli', role: 'Veli', rating: 5, text: 'Çalışma akışını takip etmek kolay.' }
    ]);
  });

  it('does not publish unverified outcome claims from legacy CMS content', () => {
    const content = parseHomePageContent({
      home_page_config: JSON.stringify({
        hero: {
          title: 'Okuma hızınızı 3 katına çıkarın',
          subtitle: 'Düzenli çalışmayla kesin sonuç garanti.'
        },
        features: {
          items: [{ title: '900+ WPM', description: 'Hızınızı garanti eder.' }]
        }
      })
    });

    expect(content.hero.title).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.title);
    expect(content.hero.subtitle).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.subtitle);
    expect(content.features.items).toEqual(DEFAULT_HOME_PAGE_CONTENT.features.items);
  });

  it('does not publish percentage outcome claims from legacy CMS content', () => {
    const content = parseHomePageContent({
      home_page_config: JSON.stringify({
        hero: { title: 'Başarı oranınızı 90% artırın' }
      })
    });

    expect(content.hero.title).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.title);
  });

  it('also filters Turkish worded percentage claims', () => {
    const content = parseHomePageContent({
      home_page_config: JSON.stringify({
        hero: { title: 'Başarı oranınızı yüzde 90 artırın' }
      })
    });

    expect(content.hero.title).toBe(DEFAULT_HOME_PAGE_CONTENT.hero.title);
  });
});
