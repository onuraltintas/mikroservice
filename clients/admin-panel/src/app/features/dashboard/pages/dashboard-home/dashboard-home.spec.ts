import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { IdentityService } from '../../../../core/services/identity.service';
import { BillingAdminService } from '../../../../core/services/billing-admin.service';
import { CoachingAdminService } from '../../../../core/services/coaching-admin.service';
import { SpeedReadingAdminService } from '../../../../core/services/speed-reading-admin.service';
import { SupportAdminService } from '../../../../core/services/support-admin.service';
import { CourseManagementAdminService } from '../../../course-management/course-management-admin.service';
import { DashboardHomeComponent } from './dashboard-home';

const overview = { waitingOrders: 4, declaredOrders: 2, withdrawalRequests: 1, unmatchedTransactions: 3, currentSubscriptions: 12,
  approvedThisMonth: 8, approvedLastMonth: 5, currency: 'TRY', endingSoon: [] };

function setup(role: string, permissions: string[], billing = vi.fn(() => of(overview))) {
  const profile = signal({ role, roles: [role], permissions, firstName: 'TEST' });
  TestBed.configureTestingModule({
    imports: [DashboardHomeComponent],
    providers: [provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile, hasPermission: (p: string) => permissions.includes(p) } },
      { provide: IdentityService, useValue: { getUserSummary: () => of({ totalUsers: 10, activeUsers: 8, inactiveUsers: 2 }), getAllRoles: () => of([{}, {}]), getPermissions: () => of([{}]) } },
      { provide: BillingAdminService, useValue: { getOverview: billing } },
      { provide: CoachingAdminService, useValue: { getOverview: () => of({ totalAssignments: 4, activeAssignments: 1, completedAssignments: 3, totalAssignmentStudents: 10,
        submittedAssignmentStudents: 7, totalGoals: 2, completedGoals: 1, upcomingSessions: 2 }) } },
      { provide: SpeedReadingAdminService, useValue: { getPlatformUsage: () => of({ activeUsers: 5, newUsers: 1, newUserDataAvailable: true, totalReadingSessions: 9,
        engagementRate: 40, dailyActiveUsers: [], activityVolume: [] }) } },
      { provide: SupportAdminService, useValue: { getAll: () => of({ items: [], totalCount: 5, pageNumber: 1, pageSize: 1 }) } },
      { provide: CourseManagementAdminService, useValue: { getOverview: () => of({ workspaceCount: 3, institutionWorkspaceCount: 1, independentTeacherWorkspaceCount: 2,
        activeMembershipCount: 0, activeStudentCount: 4, courseCount: 2, scheduledLessonCount: 0, pendingInvitationCount: 1 }) } }]
  });
  const fixture = TestBed.createComponent(DashboardHomeComponent);
  return { fixture, page: fixture.componentInstance };
}

async function settle(fixture: { detectChanges(): void }) {
  fixture.detectChanges();
  for (let i = 0; i < 3; i++) await new Promise(resolve => setTimeout(resolve));
  fixture.detectChanges();
}

describe('Yönetim paneli › Ana sayfa', () => {
  it('lists the waiting work of a system admin and adds it up in the greeting', async () => {
    const { fixture, page } = setup('SystemAdmin', [ADMIN_PERMISSIONS.supportView, ADMIN_PERMISSIONS.usersView]);
    await settle(fixture);
    expect(page.waiting().map(item => [item.label, item.value])).toEqual([
      ['Onay bekleyen ödeme', 4], ['Bildirilen havale', 2], ['Eşleşmeyen hesap hareketi', 3], ['Cayma talebi', 1], ['Açık destek talebi', 5]]);
    expect(page.waitingTotal()).toBe(15);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('ilgilenmen gereken 15 iş var');
  });

  it('shows only the sections the admin may see and names a section that failed', async () => {
    const { fixture, page } = setup('Admin', [ADMIN_PERMISSIONS.coachingView], vi.fn(() => throwError(() => new Error('x'))));
    await settle(fixture);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(page.waiting()).toEqual([]);
    expect(text).toContain('Koçluk');
    expect(text).not.toContain('Abonelikler');
    expect(text).not.toContain('Kullanıcılar');
    expect(page.failed()).toEqual([]);
  });

  it('reports a failed section without hiding the others', async () => {
    const { fixture, page } = setup('SystemAdmin', [], vi.fn(() => throwError(() => new Error('x'))));
    await settle(fixture);
    expect(page.failed()).toEqual(['Abonelik ve ödeme']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Şu bölümler yüklenemedi: Abonelik ve ödeme');
  });
});
