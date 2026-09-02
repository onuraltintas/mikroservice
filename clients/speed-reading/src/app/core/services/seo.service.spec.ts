import { TestBed } from '@angular/core/testing';
import { Meta, Title } from '@angular/platform-browser';
import { SeoService } from './seo.service';

describe('SeoService', () => {
    let service: SeoService;

    beforeEach(() => {
        document.head.innerHTML = '';
        TestBed.configureTestingModule({ providers: [SeoService, Meta, Title] });
        service = TestBed.inject(SeoService);
    });

    it('applies custom OpenGraph fields and the configured canonical URL', () => {
        service.updateTags({
            title: 'Sayfa başlığı',
            description: 'Sayfa açıklaması',
            ogTitle: 'OpenGraph başlığı',
            ogDescription: 'OpenGraph açıklaması',
            image: 'https://cdn.example.com/page.webp',
            canonicalUrl: 'https://masterhizliokuma.com/ozel-sayfa'
        });

        expect(document.querySelector('meta[property="og:title"]')?.getAttribute('content'))
            .toBe('OpenGraph başlığı');
        expect(document.querySelector('meta[property="og:description"]')?.getAttribute('content'))
            .toBe('OpenGraph açıklaması');
        expect(document.querySelector('link[rel="canonical"]')?.getAttribute('href'))
            .toBe('https://masterhizliokuma.com/ozel-sayfa');
    });
});
