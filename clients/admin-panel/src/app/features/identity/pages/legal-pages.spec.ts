import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { IdentityService, PlatformLegalPageDto } from '../../../core/services/identity.service';
import { LegalPagesComponent } from './legal-pages';

describe('LegalPagesComponent', () => {
  let fixture: ComponentFixture<LegalPagesComponent>;
  let identity: {
    getLegalPages: ReturnType<typeof vi.fn>;
    getLegalPageRevisions: ReturnType<typeof vi.fn>;
    upsertLegalPage: ReturnType<typeof vi.fn>;
    archiveLegalPage: ReturnType<typeof vi.fn>;
    restoreLegalPage: ReturnType<typeof vi.fn>;
  };

  const privacyPage: PlatformLegalPageDto = {
    slug: 'privacy', title: 'Gizlilik politikası', content: 'Onaylı metin', isPublished: true,
    version: 2, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-02-01T00:00:00Z'
  };

  beforeEach(async () => {
    identity = {
      getLegalPages: vi.fn(() => of([privacyPage])),
      getLegalPageRevisions: vi.fn(() => of([])),
      upsertLegalPage: vi.fn((slug, request) => of({ ...privacyPage, ...request, slug, version: slug === 'privacy' ? 3 : 1 })),
      archiveLegalPage: vi.fn(() => of({ ...privacyPage, isPublished: false, isArchived: true, version: 3 })),
      restoreLegalPage: vi.fn(() => of({ ...privacyPage, isPublished: false, isArchived: false, version: 4 }))
    };
    await TestBed.configureTestingModule({
      imports: [LegalPagesComponent],
      providers: [{ provide: IdentityService, useValue: identity }]
    }).compileComponents();
    fixture = TestBed.createComponent(LegalPagesComponent);
    fixture.detectChanges();
  });

  it('shows privacy and KVKK pages even when one has not been created yet', () => {
    expect(fixture.nativeElement.textContent).toContain('Gizlilik politikası');
    expect(fixture.nativeElement.textContent).toContain('KVKK aydınlatma metni');
    expect(fixture.nativeElement.textContent).toContain('Yayımlanmamış');
    expect(identity.getLegalPages).toHaveBeenCalledOnce();
  });

  it('saves the selected document as plain text and shows its saved version', () => {
    fixture.componentInstance.title.set('Gizlilik politikası');
    fixture.componentInstance.content.set('Yalnız hukukça onaylı içerik.');
    fixture.componentInstance.isPublished.set(false);
    fixture.componentInstance.save();
    fixture.detectChanges();

    expect(identity.upsertLegalPage).toHaveBeenCalledWith('privacy', {
      title: 'Gizlilik politikası', content: 'Yalnız hukukça onaylı içerik.', isPublished: false
    });
    expect(fixture.nativeElement.textContent).toContain('Sürüm 3');
  });

  it('creates a new shared legal document using its own validated slug', () => {
    fixture.componentInstance.startNewDocument();
    fixture.componentInstance.draftSlug.set('accessibility-policy');
    fixture.componentInstance.title.set('Erişilebilirlik politikası');
    fixture.componentInstance.content.set('Onaylı erişilebilirlik metni.');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[name="slug"]')).not.toBeNull();
    fixture.componentInstance.save();
    fixture.detectChanges();

    expect(identity.upsertLegalPage).toHaveBeenCalledWith('accessibility-policy', {
      title: 'Erişilebilirlik politikası', content: 'Onaylı erişilebilirlik metni.', isPublished: false
    });
    expect(fixture.nativeElement.textContent).toContain('Erişilebilirlik politikası');
    expect(fixture.nativeElement.textContent).toContain('/legal/accessibility-policy');
  });

  it('requires confirmation before archiving and provides an explicit restore action', () => {
    fixture.componentInstance.requestArchive();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Evet, arşivle');
    expect(identity.archiveLegalPage).not.toHaveBeenCalled();

    fixture.componentInstance.confirmArchive();
    fixture.detectChanges();
    expect(identity.archiveLegalPage).toHaveBeenCalledWith('privacy');
    expect(fixture.nativeElement.textContent).toContain('Arşivde');
    expect(fixture.nativeElement.textContent).toContain('Geri yükle');

    fixture.componentInstance.restoreDocument();
    fixture.detectChanges();
    expect(identity.restoreLegalPage).toHaveBeenCalledWith('privacy');
    expect(fixture.nativeElement.textContent).toContain('Mevcut sürüm 4');
  });
});
