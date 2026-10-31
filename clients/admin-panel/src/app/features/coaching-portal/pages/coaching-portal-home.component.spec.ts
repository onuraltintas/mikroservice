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
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingDailyAgendaService } from '../../../core/services/coaching-daily-agenda.service';

describe('CoachingPortalHomeComponent', () => {
  it('loads the own-student summary when a dual-role user selects student view', async () => {
    const profile = signal<UserProfile | null>({ id: 'user-1', email: 'a@test', firstName: 'Ada', lastName: 'Test', username: 'a@test', roles: ['Teacher', 'Student'], role: 'Teacher', permissions: [], product: 'coaching' });
    const service = {
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 })),
      getTeacherAssignments: vi.fn(),
      getTeacherStudents: vi.fn()
    };
    const agenda = { get: vi.fn(() => of({ mode: 'today', items: [], todayCount: 0, upcomingCount: 0, overdueCount: 0,
      totalCount: 0, pageNumber: 1, pageSize: 20, generatedAt: '2026-10-21T08:00:00Z' })) };
    TestBed.configureTestingModule({ imports: [CoachingPortalHomeComponent], providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile } },
      { provide: CoachingPortalService, useValue: service },
      { provide: CoachingDailyAgendaService, useValue: agenda },
      { provide: CoachingLearningSetupService, useValue: { get: () => of({ context: null, isReady: false }) } },
      { provide: CoachingStudyPlanningService, useValue: { list: () => of({ items: [] }), getPrimaryGoal:()=>of({goalId:null,version:null}), getAvailability: () => of({ timeZoneId: 'Europe/Istanbul' }) } },
      { provide: StaffPortalRedirectService, useValue: { redirect: vi.fn(), url: () => 'https://onuraltintas.net/staff/?product=coaching' } }
    ] });
    await TestBed.compileComponents();
    TestBed.inject(CoachingPortalViewService).select('Student');
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges(); await fixture.whenStable();
    expect(agenda.get).toHaveBeenCalledWith('today', 1);
    expect(service.getStudentAssignments).not.toHaveBeenCalled();
    expect(service.getTeacherAssignments).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Günlük işlerim');
    expect(fixture.nativeElement.textContent).toContain('Bekleyen işin yok');
    expect(fixture.nativeElement.textContent).not.toContain('Son ödevlerin');
    expect(fixture.nativeElement.querySelector('.home-hero')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/growth/goals"]')).not.toBeNull();
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
