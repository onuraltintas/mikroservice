import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlatformLegalPagesService } from './platform-legal-pages.service';

describe('PlatformLegalPagesService', () => {
  it('loads published shared legal documents through the common API route', () => {
    TestBed.configureTestingModule({
      providers: [PlatformLegalPagesService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(PlatformLegalPagesService);
    const http = TestBed.inject(HttpTestingController);

    service.getPage('kvkk').subscribe(page => expect(page.slug).toBe('kvkk'));
    const request = http.expectOne('/api/platform/legal-pages/kvkk');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, data: { slug: 'kvkk', title: 'KVKK', content: 'Onaylı metin', isPublished: true } });
    http.verify();
  });

  it('supports additional shared legal document slugs', () => {
    TestBed.configureTestingModule({
      providers: [PlatformLegalPagesService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(PlatformLegalPagesService);
    const http = TestBed.inject(HttpTestingController);

    service.getPage('accessibility-policy').subscribe(page => expect(page.slug).toBe('accessibility-policy'));
    const request = http.expectOne('/api/platform/legal-pages/accessibility-policy');
    request.flush({ success: true, data: { slug: 'accessibility-policy', title: 'Erişilebilirlik', content: 'Metin', isPublished: true } });
    http.verify();
  });
});
