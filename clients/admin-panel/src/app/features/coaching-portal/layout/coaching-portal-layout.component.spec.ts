import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingMessagesUnreadService } from '../../../core/services/coaching-messages-unread.service';
import { DarkModeService } from '../../../core/services/dark-mode.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalLayoutComponent } from './coaching-portal-layout.component';
import { CoachingWhatsNewService } from './coaching-whats-new.service';

// T2 (31.10.2026): six menu items, a five-item phone bar, notifications behind the bell and an account menu.
const flags = { showAssignments: () => true, showSessions: () => true, showFees: () => true, refresh: () => {} };
const unread = signal(0);
const messagesUnread = { count: signal(3), refresh: vi.fn() };
const notifications = signal<any[]>([]);

function create(roles: string[] = ['Student'], navigation: object = flags) {
  TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [
    provideRouter([]),
    { provide: AuthService, useValue: { userProfile: signal<any>({ roles, product: 'coaching', firstName: 'Ada', lastName: 'Kaya', id: 'u1' }), logout: vi.fn() } },
    { provide: CoachingStudentNavigationService, useValue: navigation },
    { provide: CoachingMessagesUnreadService, useValue: messagesUnread },
    { provide: NotificationService, useValue: { unreadCount: unread, notifications, fetchNotifications: vi.fn(), markAsRead: vi.fn() } },
    { provide: CoachingWhatsNewService, useValue: { visible: signal(false), dismiss: vi.fn() } },
    { provide: ToasterService, useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }
  ] });
  const fixture = TestBed.createComponent(CoachingPortalLayoutComponent);
  fixture.detectChanges();
  return fixture;
}

const hrefs = (root: HTMLElement, selector: string) =>
  [...root.querySelectorAll(selector)].map(a => a.getAttribute('href'));

describe('CoachingPortalLayoutComponent (T2 menu)', () => {
  it('shows the six student destinations in order', async () => {
    const fixture = create(); await fixture.whenStable();
    expect(hrefs(fixture.nativeElement, '.coaching-navigation a')).toEqual([
      '/coaching-portal', '/coaching-portal/plan', '/coaching-portal/assignments', '/coaching-portal/growth',
      '/coaching-portal/messages', '/coaching-portal/sessions'
    ]);
    const labels = [...fixture.nativeElement.querySelectorAll('.coaching-navigation a span:not(.coaching-badge)')].map((s: any) => s.textContent.trim());
    expect(labels).toEqual(['Bugün', 'Planım', 'Ödevlerim', 'Gelişimim', 'Mesajlar', 'Seanslar']);
  });

  it('hides assignments and sessions when the student has none', async () => {
    const fixture = create(['Student'], { ...flags, showAssignments: () => false, showSessions: () => false }); await fixture.whenStable();
    expect(hrefs(fixture.nativeElement, '.coaching-navigation a')).toEqual([
      '/coaching-portal', '/coaching-portal/plan', '/coaching-portal/growth', '/coaching-portal/messages'
    ]);
  });

  it('puts five items in the phone bar with the unread message badge', async () => {
    const fixture = create(); await fixture.whenStable();
    const links = [...fixture.nativeElement.querySelectorAll('.coaching-mobile-nav a')] as HTMLAnchorElement[];
    expect(links.map(a => a.getAttribute('href'))).toEqual([
      '/coaching-portal', '/coaching-portal/plan', '/coaching-portal/assignments', '/coaching-portal/growth', '/coaching-portal/messages'
    ]);
    expect(links[4].textContent).toContain('3');
    expect(links[4].getAttribute('aria-label')).toContain('3 okunmamış');
    expect(fixture.nativeElement.querySelector('.coaching-navigation a[href="/coaching-portal/messages"]').textContent).toContain('3');
  });

  it('keeps notifications only behind the bell, which opens a panel', async () => {
    const fixture = create(); await fixture.whenStable();
    expect(hrefs(fixture.nativeElement, '.coaching-navigation a')).not.toContain('/coaching-portal/notifications');
    expect(hrefs(fixture.nativeElement, '.coaching-mobile-nav a')).not.toContain('/coaching-portal/notifications');
    const bell = fixture.nativeElement.querySelector('button.coaching-bell') as HTMLButtonElement;
    expect(bell).toBeTruthy();
    bell.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('app-coaching-notification-panel')).toBeTruthy();
    expect(bell.getAttribute('aria-expanded')).toBe('true');
  });

  it('has no theme toggle in the header; the theme lives in settings', async () => {
    const fixture = create(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.coaching-theme-toggle')).toBeNull();
  });

  it('opens the account menu from the avatar with settings, payments, personal data and sign-out', async () => {
    const fixture = create(); await fixture.whenStable();
    const avatar = fixture.nativeElement.querySelector('button.coaching-account-toggle') as HTMLButtonElement;
    avatar.click(); fixture.detectChanges(); await fixture.whenStable();
    const menu = fixture.nativeElement.querySelector('[role=menu]') as HTMLElement;
    expect(hrefs(menu, 'a')).toEqual(['/coaching-portal/account/settings', '/coaching-portal/account/payments', '/coaching-portal/account/data']);
    expect(menu.textContent).toContain('Profil ve ayarlar');
    expect(menu.textContent).toContain('Ödemeler');
    expect(menu.textContent).toContain('Kişisel verilerim');
    const logout = [...menu.querySelectorAll('button')].find(b => b.textContent?.includes('Çıkış')) as HTMLButtonElement;
    logout.click();
    expect(TestBed.inject(AuthService).logout).toHaveBeenCalled();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=menu]')).toBeNull();
  });

  it('refreshes the unread message count on navigation', async () => {
    const fixture = create(); await fixture.whenStable();
    messagesUnread.refresh.mockClear();
    await TestBed.inject(Router).navigateByUrl('/'); await fixture.whenStable();
    expect(messagesUnread.refresh).toHaveBeenCalled();
  });

  it('closes the phone drawer with Escape and returns focus to the menu button', async () => {
    const fixture = create(); await fixture.whenStable();
    fixture.componentInstance.menuOpen.set(true); fixture.detectChanges(); await fixture.whenStable();
    const toggle = fixture.nativeElement.querySelector('.coaching-menu-toggle') as HTMLButtonElement;
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(toggle);
  });

  it('keeps the parent menu and hides the student bell and account pages for a parent', async () => {
    const fixture = create(['Parent']); await fixture.whenStable();
    expect(hrefs(fixture.nativeElement, '.coaching-navigation a')).toEqual(['/coaching-portal/parent', '/coaching-portal/messages']);
    expect(fixture.nativeElement.querySelector('button.coaching-bell')).toBeNull();
    (fixture.nativeElement.querySelector('button.coaching-account-toggle') as HTMLButtonElement).click(); fixture.detectChanges();
    const menu = fixture.nativeElement.querySelector('[role=menu]') as HTMLElement;
    expect(hrefs(menu, 'a')).toEqual([]);
    expect(menu.textContent).toContain('Çıkış');
  });

  it('keeps dual-role users in the student experience', async () => {
    const fixture = create(['Student', 'Teacher']); await fixture.whenStable();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.componentInstance.selectView('Student');
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/assignments']);
    expect(TestBed.inject(CoachingPortalViewService).current()).toBe('Student');
  });

  it('names the current page in the header from the new sections', async () => {
    const fixture = create(); await fixture.whenStable();
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/coaching-portal/growth/exams');
    (fixture.componentInstance as any).updatePageLabel();
    expect(fixture.componentInstance.pageLabel()).toBe('Gelişimim');
    vi.spyOn(router, 'url', 'get').mockReturnValue('/coaching-portal/account/payments/fees');
    (fixture.componentInstance as any).updatePageLabel();
    expect(fixture.componentInstance.pageLabel()).toBe('Ödemeler');
  });
});

describe('DarkModeService use', () => {
  it('is still injectable for the settings page', () => {
    TestBed.configureTestingModule({});
    expect(TestBed.inject(DarkModeService).setMode).toBeTypeOf('function');
  });
});
