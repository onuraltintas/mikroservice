import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { CookieConsentComponent } from '../../shared/components/cookie-consent.component';
import { SiteAnalyticsService } from './site-analytics.service';
import { SiteSeoService } from './site-seo.service';

describe('SiteAnalyticsService (O11)', () => {
  function withGaId(id: string | null) {
    document.querySelectorAll('meta[name="eduivme-ga4-id"], script[src*="googletagmanager"]').forEach(node => node.remove());
    if (id) { const meta = document.createElement('meta'); meta.name = 'eduivme-ga4-id'; meta.content = id; document.head.appendChild(meta); }
  }
  function create() {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    return TestBed.inject(SiteAnalyticsService);
  }
  beforeEach(() => {
    localStorage.clear(); sessionStorage.clear(); TestBed.resetTestingModule();
    delete (window as { gtag?: unknown }).gtag; delete (window as { dataLayer?: unknown }).dataLayer;
  });
  afterEach(() => withGaId(null));

  it('asks nothing and loads nothing without a measurement id', () => {
    withGaId(null);
    const analytics = create();
    expect(analytics.needsChoice()).toBe(false);
    analytics.track('sign_up');
    expect(document.querySelector('script[src*="googletagmanager"]')).toBeNull();
  });

  it('loads GA4 only after consent and remembers the choice', () => {
    withGaId('G-TEST1234');
    const analytics = create();
    expect(analytics.needsChoice()).toBe(true);
    expect(document.querySelector('script[src*="googletagmanager"]')).toBeNull();

    analytics.setConsent('granted');
    expect(analytics.needsChoice()).toBe(false);
    expect(document.querySelector('script[src*="googletagmanager.com/gtag/js?id=G-TEST1234"]')).toBeTruthy();
    expect(JSON.parse(localStorage.getItem('eduivme.cookie-consent.v1')!).analytics).toBe(true);
    const gtag = vi.spyOn(window, 'gtag' as never);
    analytics.track('sign_up', { method: 'password' });
    expect(gtag).toHaveBeenCalledWith('event', 'sign_up', { method: 'password' });
  });

  it('keeps analytics off after a refusal, also on the next visit', () => {
    withGaId('G-TEST1234');
    create().setConsent('denied');
    TestBed.resetTestingModule();
    const next = create();
    expect(next.consent()).toBe('denied');
    expect(next.needsChoice()).toBe(false);
    expect(document.querySelector('script[src*="googletagmanager"]')).toBeNull();
    next.reopen();
    expect(next.needsChoice()).toBe(true);
  });

  it('captures the campaign tags of the landing URL for the registration, without storing them before consent', () => {
    withGaId('G-TEST1234');
    const analytics = create();
    analytics.captureUtm('https://eduivme.com/coaching?utm_source=instagram&utm_medium=social&utm_campaign=lgs-ekim&token=secret');
    expect(analytics.registrationUtm()).toEqual({ utmSource: 'instagram', utmMedium: 'social', utmCampaign: 'lgs-ekim' });
    expect(sessionStorage.getItem('eduivme.utm.v1')).toBeNull();
    analytics.captureUtm('https://eduivme.com/auth/register/student');
    expect(analytics.registrationUtm()?.utmSource).toBe('instagram');
    analytics.setConsent('granted');
    expect(JSON.parse(sessionStorage.getItem('eduivme.utm.v1')!).utmCampaign).toBe('lgs-ekim');
  });

  it('shows the consent banner with accept and refuse', async () => {
    withGaId('G-TEST1234');
    TestBed.configureTestingModule({ imports: [CookieConsentComponent], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(CookieConsentComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="cookie-consent"]').textContent).toContain('Google Analytics');
    (fixture.nativeElement.querySelector('[data-testid="consent-deny"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="cookie-consent"]')).toBeNull();
  });
});

describe('SiteSeoService (O11)', () => {
  it('sets the preview tags, the canonical link and escaped structured data', () => {
    TestBed.resetTestingModule();
    const seo = TestBed.inject(SiteSeoService);
    seo.apply({ title: 'LGS rehberi | Eduİvme Koçluk', description: 'Özet', path: '/coaching/blog/lgs', type: 'article',
      jsonLd: { '@type': 'BlogPosting', headline: '</script><script>alert(1)</script>' } });
    expect(document.title).toBe('LGS rehberi | Eduİvme Koçluk');
    expect(document.querySelector('meta[property="og:type"]')?.getAttribute('content')).toBe('article');
    expect(document.querySelector('meta[property="og:url"]')?.getAttribute('content')).toMatch(/\/coaching\/blog\/lgs$/);
    expect(document.querySelector('meta[property="og:image"]')?.getAttribute('content')).toMatch(/\/og-coaching\.png$/);
    expect(document.querySelector('link[rel="canonical"]')?.getAttribute('href')).toMatch(/\/coaching\/blog\/lgs$/);
    const script = document.getElementById('site-jsonld')!;
    expect(script.textContent).not.toContain('</script>');
    expect(JSON.parse(script.textContent!).headline).toBe('</script><script>alert(1)</script>');

    seo.apply({ title: 'Başka', path: '/coaching/blog' });
    expect(document.getElementById('site-jsonld')).toBeNull();
    expect(document.querySelectorAll('link[rel="canonical"]')).toHaveLength(1);
  });
});
