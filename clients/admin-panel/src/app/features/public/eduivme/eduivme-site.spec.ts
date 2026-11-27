import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { EduivmeHomeComponent } from '../eduivme-home.component';
import { DEFAULT_HOME, brandForLink, siteLink } from './eduivme-site-content';
import { SitePageView, SitePublicEntry } from '../../../core/services/eduivme-site.service';
import { EduivmeProductPageComponent } from './eduivme-product-page.component';

const tick = () => new Promise(resolve => setTimeout(resolve));

describe('eduivme.com', () => {
  it('accepts only site paths, anchors and https addresses as links', () => {
    expect(siteLink('/urunler/kurs-yonetimi')).toEqual({ kind: 'route', path: '/urunler/kurs-yonetimi' });
    expect(siteLink('#urunler')).toEqual({ kind: 'anchor', fragment: 'urunler' });
    expect(siteLink('https://maxkoc.com')).toEqual({ kind: 'external', href: 'https://maxkoc.com' });
    expect(siteLink('javascript:alert(1)')).toBeNull();
    expect(siteLink('//kotu.example')).toBeNull();
  });

  it('shows the products from the site service, each card going to its own page', async () => {
    TestBed.configureTestingModule({ imports: [EduivmeHomeComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(EduivmeHomeComponent);
    fixture.detectChanges();
    const home = structuredClone(DEFAULT_HOME);
    home.hero!.title = 'Panelden gelen başlık';
    TestBed.inject(HttpTestingController).expectOne('/api/site/home').flush(home);
    await tick();
    fixture.detectChanges();
    const page: HTMLElement = fixture.nativeElement;
    expect(page.querySelector('h1')?.textContent).toContain('Panelden gelen başlık');
    const cards = Array.from(page.querySelectorAll<HTMLAnchorElement>('a.product'));
    expect(cards.map(card => card.getAttribute('href'))).toEqual(['https://maxkoc.com', 'https://masterhizliokuma.com', 'https://maxkampus.com']);
    expect(cards.map(card => card.getAttribute('data-brand'))).toEqual(['maxkoc', 'ho', 'eduivme']);
    expect(page.querySelector('[data-brand="eduivme"]')).not.toBeNull();
    expect(page.textContent).toContain('ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır.');
    expect(page.querySelectorAll('.faq details').length).toBe(4);
  });

  it('falls back to the built-in content when the site service cannot be reached', async () => {
    TestBed.configureTestingModule({ imports: [EduivmeHomeComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(EduivmeHomeComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/site/home').flush(null, { status: 502, statusText: 'Bad Gateway' });
    await tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain(DEFAULT_HOME.hero!.title);
  });

  it('says a product page is missing instead of showing a blank page', async () => {
    TestBed.configureTestingModule({ imports: [EduivmeProductPageComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ slug: 'olmayan' }) } } }] });
    const fixture = TestBed.createComponent(EduivmeProductPageComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/site/pages/olmayan').flush(null, { status: 404, statusText: 'Not Found' });
    await tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Bu ürün sayfası bulunamadı.');
  });
  it('knows which product a link leads to', () => {
    expect(brandForLink('https://maxkoc.com')).toBe('maxkoc');
    expect(brandForLink('https://veli.maxkoc.com/auth/login')).toBe('maxkoc');
    expect(brandForLink('https://masterhizliokuma.com/blog')).toBe('ho');
    expect(brandForLink('https://maxkampus.com/fiyatlar')).toBe('km');
    expect(brandForLink('https://maxkoc.com.kotu.example')).toBeNull();
    expect(brandForLink('/urunler/kurs-yonetimi')).toBeNull();
    expect(brandForLink(null)).toBeNull();
  });

  it('shows a guide page whose cards lead to the products in their own colours', async () => {
    const base = DEFAULT_HOME.products[0];
    const entry = (over: Partial<SitePublicEntry>): SitePublicEntry => ({ ...base, brand: null, highlights: [], imageUrl: null, ...over });
    const view: SitePageView = {
      page: entry({ kind: 'page', slug: 'hangi-urun-kime-uygun', title: 'Hangi ürün kime uygun?', subtitle: 'Hangi eduivme ürünü sana uygun?', linkLabel: null, linkUrl: null }),
      site: null, sections: [], features: [], steps: [], faqs: [],
      audiences: [
        entry({ id: 'a1', kind: 'audience', title: 'Plan ve koç desteği', linkLabel: 'maxkoc.com', linkUrl: 'https://maxkoc.com' }),
        entry({ id: 'a2', kind: 'audience', title: 'Okuma hızı ve anlama', linkLabel: 'masterhizliokuma.com', linkUrl: 'https://masterhizliokuma.com' }),
        entry({ id: 'a3', kind: 'audience', title: 'Kurs ve özel ders', linkLabel: 'maxkampus.com', linkUrl: 'https://maxkampus.com' }),
        entry({ id: 'a4', kind: 'audience', title: 'Öğretmen', linkLabel: 'Giriş', linkUrl: '/urunler/baska' })
      ]
    };
    TestBed.configureTestingModule({ imports: [EduivmeProductPageComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ slug: 'hangi-urun-kime-uygun' }) } } }] });
    const fixture = TestBed.createComponent(EduivmeProductPageComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/site/pages/hangi-urun-kime-uygun').flush(view);
    await tick();
    fixture.detectChanges();
    const cards = Array.from(fixture.nativeElement.querySelectorAll('.audience')) as HTMLElement[];
    expect(cards.map(card => card.getAttribute('data-brand'))).toEqual(['maxkoc', 'ho', 'eduivme', 'maxkoc']);
    expect(cards.map(card => card.classList.contains('audience--product'))).toEqual([true, true, true, false]);
    expect(cards[1].querySelector('img.audience__mark')?.getAttribute('src')).toBe('/brand/master-hizli-okuma.svg');
    expect(cards[3].querySelector('.audience__icon')).not.toBeNull();
    expect(cards[0].querySelector('a')?.textContent).toContain('(ürünün kendi sitesi)');
  });

  it('links to a guide from the products heading when the section has a link', async () => {
    TestBed.configureTestingModule({ imports: [EduivmeHomeComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(EduivmeHomeComponent);
    fixture.detectChanges();
    const home = structuredClone(DEFAULT_HOME);
    const products = home.sections.find(section => section.slug === 'products')!;
    products.linkLabel = 'Hangi ürün bana uygun?';
    products.linkUrl = '/urunler/hangi-urun-kime-uygun';
    TestBed.inject(HttpTestingController).expectOne('/api/site/home').flush(home);
    await tick();
    fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('#urunler .head__link') as HTMLAnchorElement;
    expect(link.textContent).toContain('Hangi ürün bana uygun?');
    expect(link.getAttribute('href')).toBe('/urunler/hangi-urun-kime-uygun');
  });
});
