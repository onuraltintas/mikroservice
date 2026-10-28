import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingParentService } from './coaching-parent.service';
import { ParentHomeComponent } from './parent-home.component';

describe('ParentHomeComponent', () => {
  const verified = { id: 'link-1', studentUserId: 'child-1', studentName: 'Ada Öğrenci', parentName: 'Ayşe Veli', relationship: 'Mother', status: 'Verified', requestedAt: '2026-11-01T00:00:00Z' };
  const pending = { ...verified, id: 'link-2', studentUserId: 'child-2', studentName: 'Can Öğrenci', status: 'Pending' };
  function setup(service: object) {
    TestBed.configureTestingModule({ imports: [ParentHomeComponent], providers: [provideRouter([]), { provide: CoachingParentService, useValue: service },
      { provide: ToasterService, useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }] });
    const fixture = TestBed.createComponent(ParentHomeComponent); fixture.detectChanges(); return fixture;
  }

  it('lists linked children and links still waiting for the child', () => {
    const fixture = setup({ getLinks: vi.fn(() => of([verified, pending])), requestLink: vi.fn() });
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ada Öğrenci'); expect(text).toContain('Can Öğrenci'); expect(text).toContain('onayını bekliyor');
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/parent/children/child-1"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/parent/children/child-2"]')).toBeNull();
  });

  it('sends the code with the chosen relationship and shows why a code failed', async () => {
    const service = { getLinks: vi.fn(() => of([])), requestLink: vi.fn(() => of(pending)) };
    const fixture = setup(service); const component = fixture.componentInstance;
    component.code = ' abcd-2345 '; component.relationship = 'Father';
    await component.submit();
    expect(service.requestLink).toHaveBeenCalledWith('abcd-2345', 'Father');
    expect(service.getLinks).toHaveBeenCalledTimes(2);
    service.requestLink.mockReturnValueOnce(throwError(() => ({ error: { error: { description: 'Kod geçersiz veya süresi dolmuş.' } } })) as never);
    component.code = 'WXYZ-9999'; await component.submit();
    expect(component.error()).toContain('Kod geçersiz');
  });
});
