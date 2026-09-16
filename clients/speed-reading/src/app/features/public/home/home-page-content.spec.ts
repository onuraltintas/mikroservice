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
