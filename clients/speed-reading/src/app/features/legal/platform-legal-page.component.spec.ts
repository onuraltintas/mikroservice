import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { PlatformLegalPage, PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { PlatformLegalPageComponent } from './platform-legal-page.component';

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
});
