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
