import { parseHomePageContent } from './home-page-content';

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
});
