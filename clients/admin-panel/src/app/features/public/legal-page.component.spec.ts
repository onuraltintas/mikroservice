import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { IdentityService, PlatformLegalPageDto } from '../../core/services/identity.service';
import { PlatformLegalPageComponent } from './legal-page.component';

describe('PlatformLegalPageComponent', () => {
  let fixture: ComponentFixture<PlatformLegalPageComponent>;
  let identity: { getPublicLegalPage: ReturnType<typeof vi.fn> };

  const page: PlatformLegalPageDto = {
    slug: 'privacy', title: 'Gizlilik politikası', content: 'Güvenli metin. <script>alert(1)</script>',
    isPublished: true, version: 1, createdAt: '2026-01-01T00:00:00Z'
  };

  async function createComponent(slug: string, loadPage: ReturnType<typeof vi.fn>) {
    identity = { getPublicLegalPage: loadPage };
    await TestBed.configureTestingModule({
      imports: [PlatformLegalPageComponent],
      providers: [
        { provide: IdentityService, useValue: identity },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug })) } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(PlatformLegalPageComponent);
    fixture.detectChanges();
  }

  it('renders the shared legal page as escaped plain text, never as executable HTML', async () => {
    await createComponent('privacy', vi.fn(() => of(page)));

    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Gizlilik politikası');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.querySelector('pre')?.textContent).toContain('<script>alert(1)</script>');
  });

  it('clearly reports an unpublished document when the shared Identity API returns 404', async () => {
    await createComponent('kvkk', vi.fn(() => throwError(() => ({ status: 404 }))));

    expect(fixture.nativeElement.textContent).toContain('henüz yayımlanmadı');
  });

  it('loads a published legal document with a custom slug', async () => {
    const customPage = { ...page, slug: 'accessibility-policy', title: 'Erişilebilirlik politikası' };
    const loadPage = vi.fn(() => of(customPage));
    await createComponent('accessibility-policy', loadPage);

    expect(loadPage).toHaveBeenCalledWith('accessibility-policy');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Erişilebilirlik politikası');
  });
});
