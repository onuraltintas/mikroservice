import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingParentService, ParentLink } from '../parent/coaching-parent.service';
import { StudentParentLinksComponent } from './student-parent-links.component';

// Roadmap Y7: the student hands a code to the parent and approves the link request.
describe('StudentParentLinksComponent', () => {
  const pending: ParentLink = { id: 'link-1', parentUserId: 'parent-1', parentName: 'Ayşe Veli', relationship: 'Mother', status: 'Pending', studentUserId: 'me', studentName: 'Ada Öğrenci', requestedAt: '2026-11-01T00:00:00Z' };
  function setup(service: object) {
    TestBed.configureTestingModule({ imports: [StudentParentLinksComponent], providers: [{ provide: CoachingParentService, useValue: service },
      { provide: ToasterService, useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }] });
    const fixture = TestBed.createComponent(StudentParentLinksComponent); fixture.detectChanges(); return fixture;
  }

  it('creates a code, approves a waiting parent and can end the link', async () => {
    const service = { getLinks: vi.fn(() => of([pending])), getActiveCode: vi.fn(() => of(null)), createCode: vi.fn(() => of({ code: 'ABCD-2345', expiresAt: '2026-11-09T09:00:00Z' })),
      approveLink: vi.fn(() => of(undefined)), revokeLink: vi.fn(() => of(undefined)) };
    const fixture = setup(service); const component = fixture.componentInstance;
    expect(fixture.nativeElement.textContent).toContain('Ayşe Veli'); expect(fixture.nativeElement.textContent).toContain('Anne');
    await component.createCode(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ABCD-2345');
    await component.approve(pending);
    expect(service.approveLink).toHaveBeenCalledWith('link-1');
    await component.revoke(pending);
    expect(service.revokeLink).toHaveBeenCalledWith('link-1');
    expect(service.getLinks).toHaveBeenCalledTimes(3);
  });

  // E4 #21 (live test 06.11.2026): a code made in the wizard is shown on Profilim; a new code says it replaces the old one.
  it('shows an active code made elsewhere and asks in the page before replacing it', async () => {
    const service = { getLinks: vi.fn(() => of([])), getActiveCode: vi.fn(() => of({ expiresAt: '2026-11-13T09:00:00Z', createdAt: '2026-11-06T09:00:00Z' })),
      createCode: vi.fn(() => of({ code: 'WXYZ-6789', expiresAt: '2026-11-14T09:00:00Z' })) };
    const fixture = setup(service); const el = fixture.nativeElement as HTMLElement;
    const confirm = TestBed.inject(ToasterService).confirm as ReturnType<typeof vi.fn>;
    const button = (text: string) => Array.from(el.querySelectorAll('button')).find(x => x.textContent?.trim() === text) as HTMLButtonElement | undefined;
    expect(el.textContent).toContain('Aktif bir veli kodun var: 13.11.2026 tarihine kadar, bir kez kullanılabilir.');
    expect(button('Veli kodu oluştur')).toBeUndefined();
    button('Yeni kod oluştur')!.click(); fixture.detectChanges();
    expect(service.createCode).not.toHaveBeenCalled();
    expect(el.querySelector('[data-testid="replace-code"]')!.textContent).toContain('Yeni kod oluşturursan eski kod hemen geçersiz olur.');
    button('Vazgeç')!.click(); fixture.detectChanges();
    expect(el.querySelector('[data-testid="replace-code"]')).toBeNull();
    button('Yeni kod oluştur')!.click(); fixture.detectChanges();
    button('Evet, yeni kod oluştur')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(service.createCode).toHaveBeenCalledTimes(1);
    expect(confirm).not.toHaveBeenCalled();
    expect(el.textContent).toContain('WXYZ-6789');
    expect(el.textContent).toContain('14.11.2026 tarihine kadar, bir kez kullanılabilir.');
  });

  it('offers "Veli kodu oluştur" only when there is no active code', () => {
    const service = { getLinks: vi.fn(() => of([])), getActiveCode: vi.fn(() => of(null)), createCode: vi.fn() };
    const fixture = setup(service); const el = fixture.nativeElement as HTMLElement;
    expect(Array.from(el.querySelectorAll('button')).map(x => x.textContent?.trim())).toContain('Veli kodu oluştur');
    expect(el.textContent).not.toContain('Aktif bir veli kodun var');
  });

  it('says when the earlier code expired and creates a new one without asking', async () => {
    const service = { getLinks: vi.fn(() => of([])), getActiveCode: vi.fn(() => of({ expiresAt: '2026-11-01T09:00:00Z', createdAt: '2026-10-25T09:00:00Z', expired: true })),
      createCode: vi.fn(() => of({ code: 'WXYZ-6789', expiresAt: '2026-11-14T09:00:00Z' })) };
    const fixture = setup(service); const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Veli kodunun süresi doldu (01.11.2026)');
    expect(el.textContent).not.toContain('Aktif bir veli kodun var');
    (Array.from(el.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Veli kodu oluştur') as HTMLElement).click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(service.createCode).toHaveBeenCalledTimes(1);
  });
});
