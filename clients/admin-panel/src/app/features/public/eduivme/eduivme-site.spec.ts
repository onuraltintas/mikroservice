import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { EduivmeHomeComponent } from '../eduivme-home.component';
import { DEFAULT_HOME, siteLink } from './eduivme-site-content';
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
    expect(cards.map(card => card.getAttribute('href'))).toEqual(['https://maxkoc.com', 'https://masterhizliokuma.com', '/urunler/kurs-yonetimi']);
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
});
