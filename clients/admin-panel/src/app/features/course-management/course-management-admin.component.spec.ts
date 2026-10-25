import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  CourseManagementAdminOverview,
  CourseManagementAdminService,
  CourseManagementAdminSubscriptionItem
} from './course-management-admin.service';
import { CourseManagementAdminComponent } from './course-management-admin.component';

describe('CourseManagementAdminComponent', () => {
  let fixture: ComponentFixture<CourseManagementAdminComponent>;
  let service: {
    getOverview: ReturnType<typeof vi.fn>;
    getSubscriptions: ReturnType<typeof vi.fn>;
    getSubscriptionAudit: ReturnType<typeof vi.fn>;
    assignSubscription: ReturnType<typeof vi.fn>;
  };

  const overview: CourseManagementAdminOverview = {
    workspaceCount: 8,
    institutionWorkspaceCount: 5,
    independentTeacherWorkspaceCount: 3,
    activeMembershipCount: 34,
    activeStudentCount: 27,
    courseCount: 12,
    scheduledLessonCount: 19,
    pendingInvitationCount: 2
  };

  const subscriptionItem: CourseManagementAdminSubscriptionItem = {
    workspaceId: '8a0c561e-6f67-4c8e-b369-fd7ab0e62439',
    workspaceName: 'Kuzey Eğitim',
    workspaceType: 'Institution',
    activeStudentCount: 27,
    subscription: { planName: 'Kurum Plus', activeStudentLimit: 40,
      startsAt: '2026-10-01T00:00:00Z', endsAt: '2027-10-01T00:00:00Z', accessStatus: 'Active' }
  };

  async function createComponent(
    getOverview: ReturnType<typeof vi.fn>,
    getSubscriptions = vi.fn(() => of({ totalCount: 1, skip: 0, take: 50, items: [subscriptionItem] })),
    assignSubscription = vi.fn(() => of({ workspaceId: subscriptionItem.workspaceId, ...subscriptionItem.subscription! })),
    getSubscriptionAudit = vi.fn(() => of({ totalCount: 1, skip: 0, take: 50, items: [{
      workspaceName: 'Kuzey Eğitim', workspaceType: 'Institution', action: 'Subscription.Assigned',
      occurredAt: '2026-10-13T08:30:00Z'
    }] }))
  ) {
    service = { getOverview, getSubscriptions, getSubscriptionAudit, assignSubscription };
    await TestBed.configureTestingModule({
      imports: [CourseManagementAdminComponent],
      providers: [{ provide: CourseManagementAdminService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(CourseManagementAdminComponent);
    fixture.detectChanges();
  }

  it('loads aggregate operations and plan usage without exposing owner or student records', async () => {
    await createComponent(vi.fn(() => of(overview)));

    expect(fixture.nativeElement.textContent).toContain('Çalışma alanı');
    expect(fixture.nativeElement.textContent).toContain('8');
    expect(fixture.nativeElement.textContent).toContain('27');
    expect(fixture.nativeElement.querySelector('table')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Kurum Plus');
    expect(fixture.nativeElement.textContent).not.toContain('ownerUserId');
    expect(fixture.nativeElement.textContent).not.toContain('öğrenci@example');
  });

  it('shows an error and lets a SystemAdmin retry the overview request', async () => {
    const getOverview = vi.fn()
      .mockReturnValueOnce(throwError(() => new Error('unavailable')))
      .mockReturnValueOnce(of(overview));
    await createComponent(getOverview);

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('yüklenemedi');
    fixture.nativeElement.querySelector('button')?.click();
    fixture.detectChanges();

    expect(getOverview).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('34');
  });

  it('assigns a plan with the selected workspace limit and dates', async () => {
    await createComponent(vi.fn(() => of(overview)));
    const component = fixture.componentInstance;
    component.editSubscription(subscriptionItem);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Başlangıç öncesi, bitişte/sonrasında veya plan askıya alındığında çalışma alanı salt okunur olur. Okuma ve dışa aktarma sürer; kişisel profil etkilenmez. SystemAdmin bu ekrandan planı yeniden etkinleştirebilir.'
    );
    component.planName = 'Kurum Pro';
    component.activeStudentLimit = 60;
    component.startsAt = '2026-10-01T00:00';
    component.endsAt = '2027-10-01T00:00';
    component.accessStatus = 2;

    component.saveSubscription();

    expect(service.assignSubscription).toHaveBeenCalledWith(subscriptionItem.workspaceId, {
      planName: 'Kurum Pro', activeStudentLimit: 60,
      startsAt: new Date('2026-10-01T00:00').toISOString(),
      endsAt: new Date('2027-10-01T00:00').toISOString(), accessStatus: 2
    });
    expect(component.selectedWorkspace()).toBeNull();
  });

  it('loads a read-only subscription-change audit with no person or record identifiers', async () => {
    const getSubscriptionAudit = vi.fn(() => of({ totalCount: 1, skip: 0, take: 50, items: [{
      workspaceName: 'Kuzey Eğitim', workspaceType: 'Institution', action: 'Subscription.Assigned',
      occurredAt: '2026-10-13T08:30:00Z'
    }] }));
    await createComponent(vi.fn(() => of(overview)), undefined, undefined, getSubscriptionAudit);

    const audit = fixture.nativeElement.querySelector('[aria-labelledby="course-subscription-audit-title"]');
    expect(getSubscriptionAudit).toHaveBeenCalledWith(0, 50);
    expect(audit?.textContent).toContain('Kuzey Eğitim');
    expect(audit?.textContent).toContain('Plan atandı');
    expect(audit?.textContent).not.toContain('actorUserId');
    expect(audit?.textContent).not.toContain('targetId');
  });

  it('shows an audit load error and retries the current page', async () => {
    const getSubscriptionAudit = vi.fn()
      .mockReturnValueOnce(throwError(() => new Error('unavailable')))
      .mockReturnValueOnce(of({ totalCount: 0, skip: 0, take: 50, items: [] }));
    await createComponent(vi.fn(() => of(overview)), undefined, undefined, getSubscriptionAudit);

    const audit = fixture.nativeElement.querySelector('[aria-labelledby="course-subscription-audit-title"]');
    expect(audit.querySelector('[role="alert"]')?.textContent).toContain('yüklenemedi');
    audit.querySelector('button')?.click();
    fixture.detectChanges();

    expect(getSubscriptionAudit).toHaveBeenCalledTimes(2);
    expect(audit.querySelector('[role="alert"]')).toBeNull();
    expect(audit.textContent).toContain('Henüz abonelik değişikliği yok.');
  });
});
