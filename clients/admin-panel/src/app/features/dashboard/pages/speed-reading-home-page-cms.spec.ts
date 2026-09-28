import { describe, expect, it } from 'vitest';
import {
  buildHomePageConfig,
  DEFAULT_HOME_PAGE_CARDS,
  DEFAULT_HOME_PAGE_DRAFT,
  hydrateHomePageDraft
} from './speed-reading-home-page-cms';

describe('Speed Reading home page CMS mapping', () => {
  it('prefills the structured home editor from the existing legacy landing blocks', () => {
    const content = hydrateHomePageDraft([
      { key: 'hero_title', value: 'Okuma sürenizi takip edin' },
      { key: 'features_list', value: JSON.stringify([{ icon: 'menu_book', title: 'Metin takibi', description: 'Kısa alıştırmalar.' }]) },
      { key: 'faq_items', value: JSON.stringify([{ question: 'Ölçüm nedir?', answer: 'Süre ve yanıtların kaydıdır.', category: 'Genel' }]) },
      { key: 'testimonials_title', value: 'Katılımcı görüşleri' },
      { key: 'testimonials_list', value: JSON.stringify([{ name: 'Ayşe', role: 'Öğrenci', rating: 5, text: 'Takip etmesi kolay.' }]) }
    ]);

    expect(content.draft.heroTitle).toBe('Okuma sürenizi takip edin');
    expect(content.cards.features).toEqual([{ icon: 'menu_book', title: 'Metin takibi', description: 'Kısa alıştırmalar.' }]);
    expect(content.cards.faqItems).toEqual([{ question: 'Ölçüm nedir?', answer: 'Süre ve yanıtların kaydıdır.', category: 'Genel' }]);
    expect(content.draft.testimonialsTitle).toBe('Katılımcı görüşleri');
    expect(content.cards.testimonials).toEqual([{ name: 'Ayşe', role: 'Öğrenci', rating: 5, text: 'Takip etmesi kolay.' }]);
    expect(content.draft.testimonialsVisible).toBe(true);
  });

  it('prefers saved configuration while retaining legacy lists omitted from that configuration', () => {
    const content = hydrateHomePageDraft([
      { key: 'hero_title', value: 'Eski başlık' },
      { key: 'faq_items', value: JSON.stringify([{ question: 'Eski soru', answer: 'Eski yanıt', category: 'Genel' }]) },
      { key: 'home_page_config', value: JSON.stringify({ hero: { title: 'Yeni başlık' }, faq: { title: 'Yeni SSS' } }) }
    ]);

    expect(content.draft.heroTitle).toBe('Yeni başlık');
    expect(content.draft.faqTitle).toBe('Yeni SSS');
    expect(content.cards.faqItems[0]?.question).toBe('Eski soru');
  });

  it('serializes FAQ and testimonial editors into the public home page configuration', () => {
    const cards = {
      ...DEFAULT_HOME_PAGE_CARDS,
      faqItems: [
        { question: '  Ölçüm nedir? ', answer: ' Süre ve yanıtların kaydıdır. ', category: ' Genel ' },
        { question: ' ', answer: 'Boş soru atılmalı.', category: '' }
      ],
      testimonials: [
        { name: ' Ayşe ', role: ' Öğrenci ', rating: 5, text: ' Takip etmesi kolay. ' },
        { name: '', role: '', rating: 0, text: '' }
      ]
    };

    const config = buildHomePageConfig(DEFAULT_HOME_PAGE_DRAFT, cards);

    expect(config.faq.items).toEqual([{ question: 'Ölçüm nedir?', answer: 'Süre ve yanıtların kaydıdır.', category: 'Genel' }]);
    expect(config.testimonials.items).toEqual([{ name: 'Ayşe', role: 'Öğrenci', rating: 5, text: 'Takip etmesi kolay.' }]);
    expect(config.visibility.testimonials).toBe(DEFAULT_HOME_PAGE_DRAFT.testimonialsVisible);
  });

  it('falls back safely when legacy JSON is malformed', () => {
    const content = hydrateHomePageDraft([
      { key: 'faq_items', value: '{broken' },
      { key: 'testimonials_list', value: '{broken' },
      { key: 'features_list', value: '{broken' }
    ]);

    expect(content.draft).toEqual(DEFAULT_HOME_PAGE_DRAFT);
    expect(content.cards.faqItems).toEqual([]);
    expect(content.cards.testimonials).toEqual([]);
  });
});
