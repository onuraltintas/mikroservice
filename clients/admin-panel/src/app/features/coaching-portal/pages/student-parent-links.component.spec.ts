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
    const service = { getLinks: vi.fn(() => of([pending])), createCode: vi.fn(() => of({ code: 'ABCD-2345', expiresAt: '2026-11-09T09:00:00Z' })),
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
});
