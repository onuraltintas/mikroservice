import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { CookiesComponent } from './cookies/cookies.component';
import { KvkkComponent } from './kvkk/kvkk.component';
import { PrivacyComponent } from './privacy/privacy.component';
import { TermsComponent } from './terms/terms.component';

interface LegalPageCase {
  component: Type<unknown>;
  path: string;
  slug: string;
  title: string;
}

const cases: LegalPageCase[] = [
  { component: CookiesComponent, path: '/legal/cookies', slug: 'cookies', title: 'Çerez Politikası | Master Hızlı Okuma' },
  { component: PrivacyComponent, path: '/legal/privacy', slug: 'privacy', title: 'Gizlilik Politikası | Master Hızlı Okuma' },
  { component: KvkkComponent, path: '/legal/kvkk', slug: 'kvkk', title: 'KVKK Aydınlatma Metni | Master Hızlı Okuma' },
  { component: TermsComponent, path: '/legal/terms', slug: 'speed-reading-terms', title: 'Kullanım Koşulları | Master Hızlı Okuma' },
];

const PREVIOUS = {
  title: 'Önceki sayfa',
  description: 'Önceki açıklama',
  ogTitle: 'Önceki og başlığı',
  ogUrl: 'https://masterhizliokuma.com/onceki',
};

function canonical(): string | null {
  return document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;
}

function meta(selector: string): string | null {
  return document.head.querySelector(`meta[${selector}]`)?.getAttribute('content') ?? null;
}

function setMeta(attr: 'name' | 'property', key: string, content: string | null): void {
  document.head.querySelectorAll(`meta[${attr}="${key}"]`).forEach(element => element.remove());
  if (content !== null) {
    const element = document.createElement('meta');
    element.setAttribute(attr, key);
    element.setAttribute('content', content);
    document.head.appendChild(element);
  }
}

describe('Legal pages SEO', () => {
  let snapshot: { title: string; head: string };

  beforeEach(() => {
    snapshot = { title: document.title, head: document.head.innerHTML };

    document.head.querySelectorAll('link[rel="canonical"]').forEach(element => element.remove());
    const link = document.createElement('link');
    link.setAttribute('rel', 'canonical');
    link.setAttribute('href', `${window.location.origin}/`);
    document.head.appendChild(link);

    document.title = PREVIOUS.title;
    setMeta('name', 'description', PREVIOUS.description);
    setMeta('property', 'og:title', PREVIOUS.ogTitle);
    setMeta('property', 'og:url', PREVIOUS.ogUrl);
    setMeta('property', 'og:description', null);
    setMeta('name', 'twitter:title', null);
    setMeta('name', 'twitter:description', null);
    setMeta('name', 'twitter:url', null);
  });

  afterEach(() => {
    document.title = snapshot.title;
    document.head.innerHTML = snapshot.head;
  });

  for (const page of cases) {
    describe(page.path, () => {
      let getPage: jasmine.Spy;

      function create() {
        getPage = jasmine.createSpy('getPage').and.returnValue(of({
          slug: page.slug, title: 'Yayımlanan başlık', content: '<p>Metin</p>',
          isPublished: true, version: 1, createdAt: '2026-01-01T00:00:00Z'
        }));
        TestBed.configureTestingModule({
          imports: [page.component],
          providers: [
            provideRouter([]),
            provideHttpClient(),
            provideHttpClientTesting(),
            { provide: PlatformLegalPagesService, useValue: { getPage } }
          ]
        });
        const fixture = TestBed.createComponent(page.component);
        fixture.detectChanges();
        return fixture;
      }

      it('loads its text from its own legal-pages slug', () => {
        create();
        expect(getPage).toHaveBeenCalledOnceWith(page.slug);
      });

      it('sets its own canonical, title, description and social url on init', () => {
        create();
        const ownUrl = `${window.location.origin}${page.path}`;
        const description = meta('name="description"');

        expect(canonical()).toBe(ownUrl);
        expect(document.title).toBe(page.title);
        expect(description).toBeTruthy();
        expect(description).not.toBe(PREVIOUS.description);
        expect(meta('property="og:title"')).toBe(page.title);
        expect(meta('name="twitter:title"')).toBe(page.title);
        expect(meta('property="og:description"')).toBe(description);
        expect(meta('name="twitter:description"')).toBe(description);
        expect(meta('property="og:url"')).toBe(ownUrl);
        expect(meta('name="twitter:url"')).toBe(ownUrl);
      });

      it('restores the previous canonical, title and meta tags when it is left', () => {
        create().destroy();

        expect(canonical()).toBe(`${window.location.origin}/`);
        expect(document.title).toBe(PREVIOUS.title);
        expect(meta('name="description"')).toBe(PREVIOUS.description);
        expect(meta('property="og:title"')).toBe(PREVIOUS.ogTitle);
        expect(meta('property="og:url"')).toBe(PREVIOUS.ogUrl);
        expect(meta('property="og:description"')).toBeNull();
        expect(meta('name="twitter:title"')).toBeNull();
        expect(meta('name="twitter:description"')).toBeNull();
        expect(meta('name="twitter:url"')).toBeNull();
      });
    });
  }
});
