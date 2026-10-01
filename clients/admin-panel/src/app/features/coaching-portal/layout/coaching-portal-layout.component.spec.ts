import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalLayoutComponent } from './coaching-portal-layout.component';

describe('CoachingPortalLayoutComponent multi-role view', () => {
  it('keeps dual-role users in the student experience and omits the legacy teacher navigation', () => {
    const profile = signal<any>({ roles: ['Student', 'Teacher'], product: 'coaching', firstName: 'Ada' });
    TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(CoachingPortalLayoutComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Öğretmen görünümü');
    fixture.componentInstance.selectView('Student');
    fixture.detectChanges();
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/assignments']);
    expect(fixture.nativeElement.textContent).not.toContain('Öğrenci görünümü');
    expect(fixture.nativeElement.textContent).not.toContain('Ödev Yönetimi');
    expect(TestBed.inject(CoachingPortalViewService).current()).toBe('Student');
    expect(fixture.nativeElement.textContent).not.toContain('Veli görünümü');
  });
});
