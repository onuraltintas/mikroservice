import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalLayoutComponent } from './coaching-portal-layout.component';
import { NotificationService } from '../../../core/services/notification.service';

describe('CoachingPortalLayoutComponent multi-role view', () => {
  it('exposes every student destination and closes the mobile drawer with Escape', async () => {
    const profile = signal<any>({ roles: ['Student'], product: 'coaching', firstName: 'Ada' });
    TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [
      provideRouter([]), { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const fixture = TestBed.createComponent(CoachingPortalLayoutComponent);
    fixture.detectChanges(); await fixture.whenStable();
    const links = [...fixture.nativeElement.querySelectorAll('.coaching-navigation a')] as HTMLAnchorElement[];
    expect(links).toHaveLength(15);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/messages')).toBe(true); // roadmap O3
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/goals')).toBe(true);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/weekly-plan')).toBe(true);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/setup')).toBe(true);
    expect(links.some(link => link.getAttribute('href') === '/coaching-portal/subscription')).toBe(true);
    fixture.componentInstance.menuOpen.set(true); fixture.detectChanges(); await fixture.whenStable();
    const toggle = fixture.nativeElement.querySelector('.coaching-menu-toggle') as HTMLButtonElement;
    (fixture.nativeElement.querySelector('.coaching-navigation a') as HTMLAnchorElement).focus();
    expect(fixture.nativeElement.querySelector('.coaching-menu-toggle').getAttribute('aria-expanded')).toBe('true');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(toggle);
  });
  it('keeps dual-role users in the student experience and omits the legacy teacher navigation', async () => {
    const profile = signal<any>({ roles: ['Student', 'Teacher'], product: 'coaching', firstName: 'Ada' });
    TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(CoachingPortalLayoutComponent);
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Öğretmen görünümü');
    fixture.componentInstance.selectView('Student');
    fixture.detectChanges(); await fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/assignments']);
    expect(fixture.nativeElement.textContent).not.toContain('Öğrenci görünümü');
    expect(fixture.nativeElement.textContent).not.toContain('Ödev Yönetimi');
    expect(TestBed.inject(CoachingPortalViewService).current()).toBe('Student');
    expect(fixture.nativeElement.textContent).not.toContain('Veli görünümü');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

beforeEach(() => TestBed.overrideProvider(NotificationService, { useValue: { unreadCount: signal(0) } }));

beforeEach(() => TestBed.overrideProvider(CoachingStudentNavigationService, { useValue: { showAssignments: () => true, showSessions: () => true, refresh: () => {} } }));

it('hides unused teacher areas for an independent student while retaining profile and plan entry points', async () => {
  TestBed.configureTestingModule({imports:[CoachingPortalLayoutComponent],providers:[provideRouter([]), {provide:AuthService,useValue:{userProfile:signal<any>({roles:['Student'],product:'coaching',firstName:'Ada'})}}]});
  TestBed.overrideProvider(CoachingStudentNavigationService, {useValue:{showAssignments:()=>false,showSessions:()=>false,showFees:()=>false,refresh:()=>{}}});
  const fixture=TestBed.createComponent(CoachingPortalLayoutComponent); fixture.detectChanges(); await fixture.whenStable();
  const paths=[...fixture.nativeElement.querySelectorAll('.coaching-navigation a')].map((a:any)=>a.getAttribute('href'));
  expect(paths).not.toContain('/coaching-portal/assignments');
  expect(paths).not.toContain('/coaching-portal/sessions');
  expect(paths).not.toContain('/coaching-portal/finance');
  expect(paths).toContain('/coaching-portal/subscription');
  expect(paths).toContain('/coaching-portal/setup');
  expect(paths).toContain('/coaching-portal/weekly-plan');
  expect(paths).not.toContain('/coaching-portal/profile');
  expect(paths).not.toContain('/coaching-portal/study-availability');
});
