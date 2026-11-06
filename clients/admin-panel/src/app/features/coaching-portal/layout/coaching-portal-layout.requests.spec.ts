import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingMessagesUnreadService } from '../../../core/services/coaching-messages-unread.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingPortalLayoutComponent } from './coaching-portal-layout.component';

@Component({ standalone: true, template: 'sayfa' })
class PageStub {}

const NAVIGATION = '/api/coaching/profiles/me/navigation';
const UNREAD = '/api/coaching/messages/unread-count';

/** Paket F #36 (live test): every page change asked navigation and unread-count twice and aborted one of each. */
describe('CoachingPortalLayoutComponent requests', () => {
  async function open(url: string, before?: () => void) {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(), provideHttpClientTesting(),
      provideRouter([{ path: 'coaching-portal', component: CoachingPortalLayoutComponent, children: [
        { path: '', pathMatch: 'full', component: PageStub }, { path: 'plan', component: PageStub }, { path: 'growth', component: PageStub }] }]),
      { provide: AuthService, useValue: { userProfile: signal<any>({ roles: ['Student'], product: 'coaching', firstName: 'Ada', lastName: 'Kaya', id: 'u1' }), logout: vi.fn() } },
      { provide: NotificationService, useValue: { unreadCount: signal(0), notifications: signal([]), fetchNotifications: vi.fn(), markAsRead: vi.fn() } }
    ] });
    before?.();
    const harness = await RouterTestingHarness.create(url);
    return { harness, http: TestBed.inject(HttpTestingController), router: TestBed.inject(Router) };
  }
  const labels = (root: HTMLElement) => [...root.querySelectorAll('.coaching-navigation a span:not(.coaching-badge)')].map(s => s.textContent!.trim());

  it('opening the panel asks each once, without aborting', async () => {
    const { http } = await open('/coaching-portal');
    for (const url of [NAVIGATION, UNREAD]) {
      const requests = http.match(url);
      expect(requests.length, url).toBe(1);
      expect(requests[0].cancelled).toBe(false);
    }
  });

  it('opening the panel starts both from scratch for this account (review of PR 122)', async () => {
    let spies: { unread: ReturnType<typeof vi.spyOn>; navigation: ReturnType<typeof vi.spyOn> } | undefined;
    await open('/coaching-portal', () => spies = {
      unread: vi.spyOn(TestBed.inject(CoachingMessagesUnreadService), 'refresh'),
      navigation: vi.spyOn(TestBed.inject(CoachingStudentNavigationService), 'refresh') });
    expect(spies!.unread).toHaveBeenCalledWith({ reset: true });
    expect(spies!.navigation).toHaveBeenCalledWith({ reset: true });
  });

  it('each page change asks each once; a change of only the query string asks nothing', async () => {
    const { http, router } = await open('/coaching-portal');
    http.expectOne(NAVIGATION).flush({ data: { showAssignments: false, showSessions: false } });
    http.expectOne(UNREAD).flush({ count: 0 });
    await router.navigateByUrl('/coaching-portal/plan');
    expect(http.match(NAVIGATION).length).toBe(1);
    expect(http.match(UNREAD).length).toBe(1);
    await router.navigateByUrl('/coaching-portal/plan?plan=p1');
    http.expectNone(NAVIGATION); http.expectNone(UNREAD);
  });

  it('shows only the fixed items until the navigation answer arrives', async () => {
    const { harness, http } = await open('/coaching-portal');
    harness.detectChanges();
    expect(labels(harness.fixture.nativeElement)).toEqual(['Bugün', 'Planım', 'Gelişimim', 'Mesajlar']);
    http.expectOne(NAVIGATION).flush({ data: { showAssignments: true, showSessions: false } });
    harness.detectChanges();
    expect(labels(harness.fixture.nativeElement)).toEqual(['Bugün', 'Planım', 'Ödevlerim', 'Gelişimim', 'Mesajlar']);
  });

  it('a failed navigation answer keeps Ödevlerim and Seanslar reachable', async () => {
    const { harness, http } = await open('/coaching-portal');
    http.expectOne(NAVIGATION).flush({}, { status: 503, statusText: 'Unavailable' });
    harness.detectChanges();
    expect(labels(harness.fixture.nativeElement)).toEqual(['Bugün', 'Planım', 'Ödevlerim', 'Gelişimim', 'Mesajlar', 'Seanslar']);
  });
});
