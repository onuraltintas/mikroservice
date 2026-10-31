import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingMessagesUnreadService } from '../../../core/services/coaching-messages-unread.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { DarkModeService } from '../../../core/services/dark-mode.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingNotificationPanelComponent, groupNotificationsByDay } from './coaching-notification-panel.component';
import { CoachingWhatsNewCardComponent } from './coaching-whats-new-card.component';
import { CoachingWhatsNewService } from './coaching-whats-new.service';
import { StudentHubComponent } from './student-hub.component';
import { StudentThemeSettingComponent } from './student-theme-setting.component';

const profile = signal<any>({ id: 'student-1', roles: ['Student'] });

describe('CoachingWhatsNewService (one-time intro card)', () => {
  const key = 'coaching-whats-new-v1:student-1';
  beforeEach(() => { localStorage.removeItem(key); TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: { userProfile: profile } }] }); });

  it('shows the card until the student dismisses it, per account', () => {
    const service = TestBed.inject(CoachingWhatsNewService);
    expect(service.visible()).toBe(true);
    service.dismiss();
    expect(service.visible()).toBe(false);
    expect(localStorage.getItem(key)).toBe('seen');
  });

  it('stays hidden when it was dismissed before', () => {
    localStorage.setItem(key, 'seen');
    expect(TestBed.inject(CoachingWhatsNewService).visible()).toBe(false);
  });

  it('does not fail when storage is blocked; the card closes for this visit', () => {
    const get = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    const set = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    const service = TestBed.inject(CoachingWhatsNewService);
    expect(service.visible()).toBe(true);
    expect(() => service.dismiss()).not.toThrow();
    expect(service.visible()).toBe(false);
    get.mockRestore(); set.mockRestore();
  });
});

describe('CoachingWhatsNewCardComponent', () => {
  it('explains the new menu in short Turkish and closes', () => {
    const service = { visible: signal(true), dismiss: vi.fn() };
    TestBed.configureTestingModule({ imports: [CoachingWhatsNewCardComponent], providers: [provideRouter([]), { provide: CoachingWhatsNewService, useValue: service }] });
    const fixture = TestBed.createComponent(CoachingWhatsNewCardComponent); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Yeni');
    for (const word of ['Bugün', 'Planım', 'Gelişimim', 'Mesajlar']) expect(text).toContain(word);
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(service.dismiss).toHaveBeenCalled();
    service.visible.set(false); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('section')).toBeNull();
  });
});

describe('CoachingNotificationPanelComponent', () => {
  it('groups notifications by Turkish day: Bugün, Dün, then the date', () => {
    const now = new Date('2026-10-31T15:00:00+03:00');
    const groups = groupNotificationsByDay([
      { id: 'a', title: 'A', message: '', type: 'X', isRead: false, createdAt: new Date('2026-10-31T09:00:00+03:00') },
      { id: 'b', title: 'B', message: '', type: 'X', isRead: true, createdAt: new Date('2026-10-30T22:00:00+03:00') },
      { id: 'c', title: 'C', message: '', type: 'X', isRead: true, createdAt: new Date('2026-10-28T10:00:00+03:00') }
    ] as any, now);
    expect(groups.map(g => [g.label, g.items.map(i => i.id)])).toEqual([['Bugün', ['a']], ['Dün', ['b']], ['28 Ekim Çarşamba', ['c']]]);
  });

  it('puts the newest day first and the newest notification first within a day', () => {
    const now = new Date('2026-10-31T15:00:00+03:00');
    const groups = groupNotificationsByDay([
      { id: 'old', title: 'A', message: '', type: 'X', isRead: true, createdAt: new Date('2026-10-30T08:00:00+03:00') },
      { id: 'morning', title: 'B', message: '', type: 'X', isRead: true, createdAt: new Date('2026-10-31T08:00:00+03:00') },
      { id: 'late', title: 'C', message: '', type: 'X', isRead: true, createdAt: new Date('2026-10-30T21:00:00+03:00') },
      { id: 'noon', title: 'D', message: '', type: 'X', isRead: false, createdAt: new Date('2026-10-31T12:00:00+03:00') }
    ] as any, now);
    expect(groups.map(g => [g.label, g.items.map(i => i.id)])).toEqual([['Bugün', ['noon', 'morning']], ['Dün', ['late', 'old']]]);
  });

  it('lists at most ten, links to the full page and emits close', () => {
    const items = Array.from({ length: 12 }, (_, i) => ({ id: `n${i}`, title: `Bildirim ${i}`, message: 'm', type: 'X', isRead: i > 2, createdAt: new Date() }));
    TestBed.configureTestingModule({ imports: [CoachingNotificationPanelComponent], providers: [provideRouter([]),
      { provide: NotificationService, useValue: { notifications: signal(items), unreadCount: signal(3), fetchNotifications: vi.fn(), markAsRead: vi.fn(() => of(null)) } }] });
    const fixture = TestBed.createComponent(CoachingNotificationPanelComponent);
    const closed = vi.fn(); fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('li').length).toBe(10);
    const all = fixture.nativeElement.querySelector('a[href="/coaching-portal/notifications"]') as HTMLAnchorElement;
    expect(all.textContent).toContain('Tümünü gör');
    (fixture.nativeElement.querySelector('button.close') as HTMLButtonElement).click();
    expect(closed).toHaveBeenCalled();
  });

  it('shows a friendly empty state', () => {
    TestBed.configureTestingModule({ imports: [CoachingNotificationPanelComponent], providers: [provideRouter([]),
      { provide: NotificationService, useValue: { notifications: signal([]), unreadCount: signal(0), fetchNotifications: vi.fn(), markAsRead: vi.fn() } }] });
    const fixture = TestBed.createComponent(CoachingNotificationPanelComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Yeni bildirimin yok');
  });
});

describe('StudentHubComponent', () => {
  function hub(name: string, navigation = { showFees: () => true }) {
    TestBed.configureTestingModule({ imports: [StudentHubComponent], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { data: { hub: name } } } },
      { provide: CoachingStudentNavigationService, useValue: navigation }] });
    const fixture = TestBed.createComponent(StudentHubComponent); fixture.detectChanges();
    return fixture;
  }

  it('renders the hub tabs as links with an outlet', () => {
    const fixture = hub('growth');
    const links = [...fixture.nativeElement.querySelectorAll('nav a')] as HTMLAnchorElement[];
    expect(links.map(a => a.getAttribute('href'))).toEqual(['/coaching-portal/growth/exams', '/coaching-portal/growth/goals', '/coaching-portal/growth/week']);
    expect(fixture.nativeElement.querySelector('nav').getAttribute('aria-label')).toBe('Gelişimim bölümleri');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });

  it('shows no tab strip for a hub that is one page (Ödemeler)', () => {
    const fixture = hub('payments');
    expect(fixture.nativeElement.querySelector('nav')).toBeNull();
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});

describe('StudentThemeSettingComponent', () => {
  it('offers light, dark and system and saves the choice', () => {
    const theme = { mode: signal<'light' | 'dark' | 'system'>('system'), setMode: vi.fn() };
    TestBed.configureTestingModule({ imports: [StudentThemeSettingComponent], providers: [{ provide: DarkModeService, useValue: theme }] });
    const fixture = TestBed.createComponent(StudentThemeSettingComponent); fixture.detectChanges();
    const radios = [...fixture.nativeElement.querySelectorAll('input[type=radio]')] as HTMLInputElement[];
    expect(radios.map(r => r.value)).toEqual(['light', 'dark', 'system']);
    expect(radios[2].checked).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Sistemi izle');
    radios[1].click();
    expect(theme.setMode).toHaveBeenCalledWith('dark');
  });
});

describe('CoachingMessagesUnreadService', () => {
  it('reads the unread message count and falls back to zero on errors', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: { userProfile: profile } }] });
    const service = TestBed.inject(CoachingMessagesUnreadService);
    const http = TestBed.inject(HttpTestingController);
    service.refresh();
    http.expectOne('/api/coaching/messages/unread-count').flush({ count: 4 });
    expect(service.count()).toBe(4);
    service.refresh();
    http.expectOne('/api/coaching/messages/unread-count').flush('x', { status: 500, statusText: 'err' });
    expect(service.count()).toBe(0);
    http.verify();
  });
});
