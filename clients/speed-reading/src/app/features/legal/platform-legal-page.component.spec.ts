import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { PlatformLegalPage, PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { PlatformLegalPageComponent } from './platform-legal-page.component';
import { PUBLIC_SITE_URL } from '../../core/utils/public-site-url';

describe('PlatformLegalPageComponent', () => {
  let fixture: ComponentFixture<PlatformLegalPageComponent>;
  let legalPages: { getPage: jasmine.Spy };

  async function createComponent(slug: string, result: Observable<PlatformLegalPage>) {
    legalPages = { getPage: jasmine.createSpy('getPage').and.returnValue(result) };
    await TestBed.configureTestingModule({
      imports: [PlatformLegalPageComponent],
      providers: [
        { provide: PlatformLegalPagesService, useValue: legalPages },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug })) } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(PlatformLegalPageComponent);
    fixture.detectChanges();
  }

  it('renders additional shared legal documents as plain text', async () => {
    await createComponent('accessibility-policy', of({
      slug: 'accessibility-policy', title: 'Erişilebilirlik politikası',
      content: 'Metin <script>çalışmaz</script>', isPublished: true, version: 1,
      createdAt: '2026-01-01T00:00:00Z'
    }));

    expect(legalPages.getPage).toHaveBeenCalledWith('accessibility-policy');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Erişilebilirlik politikası');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<script>çalışmaz</script>');
  });

  it('shows a not-published state when the shared document does not exist', async () => {
    await createComponent('accessibility-policy', throwError(() => ({ status: 404 })));

    expect(fixture.nativeElement.textContent).toContain('henüz yayımlanmadı');
  });

  it('gives a published document its own title, description and canonical address', async () => {
    await createComponent('speed-reading-newsletter-consent', of({
      slug: 'speed-reading-newsletter-consent', title: 'Bülten onay metni',
      content: 'Bu metin bülten aboneliği için açık rızanızı açıklar.', isPublished: true, version: 1,
      createdAt: '2026-01-01T00:00:00Z'
    }));

    expect(document.title).toBe('Bülten onay metni | Master Hızlı Okuma');
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href'))
      .toBe(`${PUBLIC_SITE_URL}/legal/speed-reading-newsletter-consent`);
    expect(document.head.querySelector('meta[name="description"]')?.getAttribute('content'))
      .toBe('Bu metin bülten aboneliği için açık rızanızı açıklar.');
    fixture.destroy();
  });

  it('keeps an unpublished document out of search results', async () => {
    await createComponent('accessibility-policy', throwError(() => ({ status: 404 })));

    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toContain('noindex');
    fixture.destroy();
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).not.toContain('noindex');
  });
});
