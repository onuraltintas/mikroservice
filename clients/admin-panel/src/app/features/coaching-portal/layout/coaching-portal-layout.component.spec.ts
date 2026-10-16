import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalLayoutComponent } from './coaching-portal-layout.component';

describe('CoachingPortalLayoutComponent multi-role view', () => {
  it('exposes every student destination and closes the mobile drawer with Escape', () => {
    const profile = signal<any>({ roles: ['Student'], product: 'coaching', firstName: 'Ada' });
    TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [
      provideRouter([]), { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const fixture = TestBed.createComponent(CoachingPortalLayoutComponent);
    fixture.detectChanges();
    const links = [...fixture.nativeElement.querySelectorAll('.coaching-navigation a')] as HTMLAnchorElement[];
    expect(links).toHaveLength(12);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/profile')).toBe(true);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/subscription')).toBe(true);
    fixture.componentInstance.menuOpen.set(true); fixture.detectChanges();
    const toggle = fixture.nativeElement.querySelector('.coaching-menu-toggle') as HTMLButtonElement;
    (fixture.nativeElement.querySelector('.coaching-navigation a') as HTMLAnchorElement).focus();
    expect(fixture.nativeElement.querySelector('.coaching-menu-toggle').getAttribute('aria-expanded')).toBe('true');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(toggle);
  });
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
