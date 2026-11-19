import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface CourseManagementAdminOverview {
  workspaceCount: number;
  institutionWorkspaceCount: number;
  independentTeacherWorkspaceCount: number;
  activeMembershipCount: number;
  activeStudentCount: number;
  courseCount: number;
  scheduledLessonCount: number;
  pendingInvitationCount: number;
}

export interface CourseManagementAdminSubscriptionDetails {
  planName: string;
  activeStudentLimit: number;
  startsAt: string;
  endsAt: string;
  accessStatus: 'Active' | 'Suspended';
}

export interface CourseManagementAdminSubscriptionItem {
  workspaceId: string;
  workspaceName: string;
  workspaceType: 'Institution' | 'IndependentTeacher';
  activeStudentCount: number;
  subscription: CourseManagementAdminSubscriptionDetails | null;
}

export interface CourseManagementAdminSubscriptionPage {
  totalCount: number;
  skip: number;
  take: number;
  items: CourseManagementAdminSubscriptionItem[];
}

export interface CourseManagementAdminSubscriptionAuditItem {
  workspaceName: string;
  workspaceType: 'Institution' | 'IndependentTeacher';
  action: 'Subscription.Assigned' | 'Subscription.Updated';
  occurredAt: string;
}

export interface CourseManagementAdminSubscriptionAuditPage {
  totalCount: number;
  skip: number;
  take: number;
  items: CourseManagementAdminSubscriptionAuditItem[];
}

@Injectable({ providedIn: 'root' })
export class CourseManagementAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/course-management/v1/admin`;

  getOverview() {
    return this.http.get<CourseManagementAdminOverview>(`${this.url}/overview`);
  }
  getSubscriptions(skip: number, take: number) {
    return this.http.get<CourseManagementAdminSubscriptionPage>(`${this.url}/subscriptions?skip=${skip}&take=${take}`);
  }
  getSubscriptionAudit(skip: number, take: number) {
    return this.http.get<CourseManagementAdminSubscriptionAuditPage>(
      `${this.url}/subscription-audit?skip=${skip}&take=${take}`);
  }
  assignSubscription(workspaceId: string, subscription: {
    planName: string; activeStudentLimit: number; startsAt: string; endsAt: string; accessStatus: 1 | 2;
  }) {
    return this.http.put<CourseManagementAdminSubscriptionDetails>(
      `${this.url}/workspaces/${workspaceId}/subscription`, subscription);
  }
}

export interface CourseManagementAdminWorkspaceRow {
  id: string; name: string; type: 'Institution' | 'IndependentTeacher'; createdAt: string;
  teacherCount: number; activeStudentCount: number; lastActivityAt: string | null; isReadOnly: boolean;
}

export type SupportSection = 'Students' | 'Schedule' | 'Finance' | 'Settings';

export interface CourseManagementSupportGrant {
  id: string; workspaceId: string; adminUserId: string; reason: string;
  status: 'Requested' | 'Active' | 'Ended'; sections: SupportSection[]; durationHours: number;
  requestedAt: string; approvedAt: string | null; expiresAt: string | null; endedAt: string | null; endReason: string | null;
}

export interface CourseManagementAdminWorkspaceDetail {
  id: string; name: string; type: 'Institution' | 'IndependentTeacher'; createdAt: string; isReadOnly: boolean;
  teacherCount: number; activeStudentCount: number; courseCount: number; lessonsLast30Days: number;
  email: { switchedOn: boolean; sentLast30Days: number; recipientsLast30Days: number; skippedLast30Days: number; optedOutAddresses: number };
  supportAccess: CourseManagementSupportGrant[];
}

/** Institutions and owner-approved support access (19.11.2026). */
@Injectable({ providedIn: 'root' })
export class CourseManagementInstitutionsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/course-management/v1/admin/workspaces`;

  list(query: string, skip: number, take: number) {
    const search = query.trim() ? `&query=${encodeURIComponent(query.trim())}` : '';
    return this.http.get<{ total: number; items: CourseManagementAdminWorkspaceRow[] }>(`${this.url}?skip=${skip}&take=${take}${search}`);
  }
  detail(id: string) { return this.http.get<CourseManagementAdminWorkspaceDetail>(`${this.url}/${id}`); }
  requestAccess(id: string, reason: string) {
    return this.http.post<CourseManagementSupportGrant>(`${this.url}/${id}/support-access`, { reason });
  }
  endAccess(id: string, grantId: string) {
    return this.http.post<CourseManagementSupportGrant>(`${this.url}/${id}/support-access/${grantId}/end`, {});
  }
  section(id: string, section: SupportSection) {
    const path = { Students: 'students', Schedule: 'lessons', Finance: 'finance', Settings: 'settings' }[section];
    return this.http.get<unknown>(`${this.url}/${id}/support/${path}`);
  }
}
