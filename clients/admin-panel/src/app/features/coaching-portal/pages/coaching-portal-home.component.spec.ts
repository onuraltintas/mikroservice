import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { CoachingPortalHomeComponent } from './coaching-portal-home.component';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudentTodayService, StudentTodayView } from '../../../core/services/coaching-student-today.service';
import { CoachingWhatsNewService } from '../layout/coaching-whats-new.service';

describe('CoachingPortalHomeComponent', () => {
  const view = (over: Partial<StudentTodayView> = {}): StudentTodayView => ({
    today: '2026-10-21', timeZoneId: 'Europe/Istanbul', generatedAt: '2026-10-21T08:00:00Z',
    nextStep: { item: { kind: 'assignment', recordId: 'a-late', title: 'Türev ödevi', date: '2026-10-20', startMinute: 1080, minutes: 40, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }, overdue: true },
    items: [
      { kind: 'study', recordId: 's-1', title: 'Paragraf', date: '2026-10-21', startMinute: 840, minutes: 30, timeZoneId: 'Europe/Istanbul', planId: 'p-1', weekStart: null },
      { kind: 'assignment', recordId: 'a-1', title: 'Okuma ödevi', date: '2026-10-21', startMinute: 1020, minutes: null, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }
    ],
    overdue: [{ kind: 'assignment', recordId: 'a-late', title: 'Türev ödevi', date: '2026-10-20', startMinute: 1080, minutes: 40, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }],
    todayCount: 2, overdueCount: 1, upcomingCount: 3,
    exam: { name: 'YKS', date: '2027-06-19', daysLeft: 241, source: 'Catalog' },
    streak: { currentStreak: 6, longestStreak: 11, activeToday: true },
    week: { weekStart: '2026-10-19', planned: 8, completed: 3 },
    goal: { goalId: 'g-1', title: 'ODTÜ Bilgisayar', examCode: 'TYT', targetNet: 90, latestNet: 80, remainingNet: 10 },
    weeklyReview: { needed: false, weekStart: '2026-10-12' },
    nextSession: null, hasActivePlan: true, ...over
  });

  async function render(today: StudentTodayView, setupReady = true) {
    const profile = signal<UserProfile | null>({ id: 'user-1', email: 'a@test', firstName: 'Elif', lastName: 'Test', username: 'a@test', roles: ['Student'], role: 'Student', permissions: [], product: 'coaching' });
    const api = { get: vi.fn(() => of(today)) };
    TestBed.configureTestingModule({ imports: [CoachingPortalHomeComponent], providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile } },
      { provide: CoachingStudentTodayService, useValue: api },
      { provide: CoachingLearningSetupService, useValue: { get: () => of({ context: null, isReady: setupReady }) } },
      { provide: CoachingWhatsNewService, useValue: { visible: () => true, dismiss: vi.fn() } },
      { provide: StaffPortalRedirectService, useValue: { redirect: vi.fn(), url: () => '' } }
    ] });
    await TestBed.compileComponents();
    TestBed.inject(CoachingPortalViewService).select('Student');
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, api, el: fixture.nativeElement as HTMLElement };
  }

  it('opens with the next step: one summary call, the overdue assignment first with a single action', async () => {
    const { api, el } = await render(view());
    expect(api.get).toHaveBeenCalledTimes(1);
    const next = el.querySelector('[data-testid="next-step"]')!;
    expect(next).not.toBeNull();
    // The next step is the first card on the page (after the one-time intro card).
    const sections = Array.from(el.querySelectorAll('[data-home-block]')).map(x => x.getAttribute('data-home-block'));
    expect(sections[0]).toBe('next-step');
    expect(next.textContent).toContain('Türev ödevi');
    expect(next.textContent).toContain('Gecikti');
    const action = next.querySelectorAll('a,button');
    expect(action.length).toBe(1);
    expect(action[0].getAttribute('href')).toBe('/coaching-portal/assignments/a-late');
  });

  it('lists today with overdue work merged and marked, and keeps upcoming collapsed', async () => {
    const { el } = await render(view());
    const rows = Array.from(el.querySelectorAll('[data-testid="today-list"] [data-record]'));
    expect(rows.map(x => x.getAttribute('data-record'))).toEqual(['a-late', 's-1', 'a-1']);
    expect(rows[0].textContent).toContain('Gecikti');
    expect(rows[1].textContent).not.toContain('Gecikti');
    expect(el.textContent).toContain('Yaklaşan 3');
    expect(el.querySelector('app-student-daily-agenda')).toBeNull();
  });

  it('shows the exam countdown, streak, week ring and the goal gap in one line', async () => {
    const { el } = await render(view());
    const strip = el.querySelector('[data-testid="today-strip"]')!.textContent!;
    expect(strip).toContain('241');
    expect(strip).toContain('YKS');
    expect(strip).toContain('6');
    expect(el.querySelector('cs-progress-ring')!.getAttribute('ng-reflect-value') ?? '3').toBe('3');
    expect(el.textContent).toContain('Hedefine 10 net kaldı');
    expect(el.querySelector('a[href="/coaching-portal/growth/goals"]')).not.toBeNull();
  });

  it('shows the weekly review and the next session only when there is one', async () => {
    let r = await render(view());
    expect(r.el.querySelector('[data-testid="weekly-review"]')).toBeNull();
    expect(r.el.querySelector('[data-testid="next-session"]')).toBeNull();
    TestBed.resetTestingModule();
    r = await render(view({ weeklyReview: { needed: true, weekStart: '2026-10-12' },
      nextSession: { id: 'ses-1', title: 'Haftalık görüşme', scheduledDate: '2026-10-22T07:00:00Z', durationMinutes: 45, meetingLink: null } }));
    expect(r.el.querySelector('[data-testid="weekly-review"]')!.textContent).toContain('Haftanı değerlendir');
    expect(r.el.querySelector('[data-testid="next-session"]')!.textContent).toContain('Haftalık görüşme');
  });

  it('drops the welcome block, the ready setup card, the shortcut stack and the extra refresh buttons', async () => {
    const { el } = await render(view());
    expect(el.textContent).not.toContain('Merhaba');
    expect(el.textContent).not.toContain('Çalışma düzenin');
    expect(el.textContent).not.toContain('Plan sihirbazı');
    expect(Array.from(el.querySelectorAll('button')).filter(x => x.textContent?.includes('Yenile')).length).toBeLessThanOrEqual(0);
    expect(el.querySelector('app-coaching-whats-new-card')).not.toBeNull();
  });

  it('asks to finish setup only when it is not ready, and offers a first plan when there is none', async () => {
    const { el } = await render(view({ hasActivePlan: false, nextStep: null, items: [], overdue: [], todayCount: 0, overdueCount: 0 }), false);
    expect(el.textContent).toContain('Çalışma düzenini kur');
    expect(el.querySelector('a[href="/coaching-portal/plan/wizard"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="next-step"]')).toBeNull();
  });

  it('sends teacher-only users to the new staff portal instead of rendering the legacy dashboard', async () => {
    const profile = signal<UserProfile | null>({
      id: 'teacher-1', email: 'teacher@example.test', firstName: 'Ada', lastName: 'Koç',
      username: 'teacher@example.test', roles: ['Teacher'], role: 'Teacher', permissions: [], product: 'coaching'
    });
    const service = {
      getTeacherAssignments: vi.fn(() => of({
        items: [{ id: 'a1', title: 'Ödev 1', type: 'Digital', dueDate: '2030-01-01', status: 'Active',
          totalStudents: 4, submittedCount: 2, createdAt: '2029-12-01' }],
        pageNumber: 1, pageSize: 5, totalCount: 23, totalPages: 5
      })),
      getTeacherStudents: vi.fn(() => of({
        items: [{ userId: 'student-1' }], pageNumber: 1, pageSize: 1, totalCount: 41, totalPages: 41
      }))
    };
    const redirect = vi.fn();
    TestBed.configureTestingModule({
      imports: [CoachingPortalHomeComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: StaffPortalRedirectService, useValue: { redirect, url: () => 'https://onuraltintas.net/staff/?product=coaching' } }
      ]
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges(); await fixture.whenStable();

    expect(redirect).toHaveBeenCalledWith('coaching');
    expect(service.getTeacherAssignments).not.toHaveBeenCalled();
    expect(service.getTeacherStudents).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Toplam ödev');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

beforeEach(() => TestBed.overrideProvider(CoachingStudentNavigationService, { useValue: { showAssignments: () => true, showSessions: () => true, refresh: () => {} } }));
