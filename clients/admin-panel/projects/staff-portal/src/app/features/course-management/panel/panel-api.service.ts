import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CourseLesson } from '../course-management.service';

/** Endpoints the institution panel (19.11.2026) adds on top of CourseManagementService. */
export interface PanelMember { userId: string; role: 'Owner' | 'Teacher' | 'Student'; isActive: boolean; displayName: string | null; }
export interface PanelOverviewLesson {
  id: string; courseName: string; groupName: string | null; roomName: string | null; teacherName: string | null; teacherUserId: string;
  startsAt: string; endsAt: string; studentCount: number; attendanceTaken: boolean; status: 'Scheduled' | 'InProgress' | 'Cancelled' | 'Completed';
}
export interface PanelOverview {
  today: string; role: 'Owner' | 'Teacher'; todayLessons: PanelOverviewLesson[];
  lessonsWaitingForAttendance: { id: string; courseName: string; startsAt: string; groupName: string | null }[];
  finance: { overdueCount: number; overdueStudentCount: number; overdueAmount: number; monthCollections: number } | null;
  emails: { sent: number; skipped: number; byKind: { kind: string; count: number }[] } | null;
  supportAccess: { pendingRequest: boolean; active: boolean } | null;
}
export interface PanelStudentRow {
  id: string; displayName: string; email: string | null; phoneNumber: string | null; isActive: boolean; emailsMuted: boolean;
  createdAt: string; groups: { id: string; name: string; courseName: string }[]; guardianCount: number;
  guardianWithoutEmail: boolean; overdueAmount: number | null;
}
export interface PanelStudentPage { total: number; skip: number; take: number; items: PanelStudentRow[]; }
export interface PanelStudentFilter {
  q?: string; courseId?: string; groupId?: string; teacherId?: string; status?: 'active' | 'archived' | 'all';
  overdue?: boolean | null; sort?: 'name' | '-name' | 'created' | '-created'; skip?: number; take?: number;
}
export type CompletionMark = 0 | 1 | 2 | 3;
export interface PanelCompletion { studentId: string; studentName: string; completion: 'NotMarked' | 'Done' | 'Partial' | 'NotDone'; note: string | null; markedAt: string | null; }
export interface PanelEmailRule { kind: string; kindValue: number; enabled: boolean; audience: number; note: string | null; }
export interface PanelEmailSettings {
  rules: PanelEmailRule[]; installmentDaysBefore: number; installmentOnDueDay: boolean; overdueDaysAfter: number; sendHour: number;
  replyToEmail: string | null; informedConsentAt: string | null;
}
export interface PanelEmailPreview { institutionName: string; subject: string; title: string; paragraphs: string[]; note: string | null; }
export interface PanelSentEmail {
  id: string; kind: string; studentId: string; studentName: string; status: 'Sent' | 'Skipped'; subject: string | null;
  recipients: string | null; recipientCount: number; skipReason: string | null; processedAt: string | null;
}
export interface PanelSupportGrant {
  id: string; reason: string; status: 'Requested' | 'Active' | 'Ended'; sections: string[]; durationHours: number;
  requestedAt: string; approvedAt: string | null; expiresAt: string | null; endedAt: string | null; endReason: string | null;
}
export interface PanelSupportView { grants: PanelSupportGrant[]; logs: { grantId: string; section: string; what: string; occurredAt: string }[]; }
export interface SeriesRequest {
  courseId: string; groupId: string | null; studentId: string | null; teacherUserId: string; startsOn: string; endsOn: string;
  startTime: string; durationMinutes: number; daysOfWeekMask: number; roomId: string | null;
  repeatEveryWeeks: number; monthlyWeekOfMonth: number | null;
}
export interface SeriesPreview { sessionCount: number; conflictCount: number; occurrences: { startsAt: string; endsAt: string; conflicts: string[] }[]; }

@Injectable({ providedIn: 'root' })
export class CoursePanelApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/course-management/v1`;
  private ws(id: string) { return `${this.base}/workspaces/${id}`; }

  overview(id: string) { return firstValueFrom(this.http.get<PanelOverview>(`${this.ws(id)}/overview`)); }
  members(id: string) { return firstValueFrom(this.http.get<PanelMember[]>(`${this.ws(id)}/members`)); }
  inviteTeacher(id: string, email: string) {
    return firstValueFrom(this.http.post<unknown>(`${this.ws(id)}/teacher-invitations`, { id: crypto.randomUUID(), email }));
  }
  searchStudents(id: string, filter: PanelStudentFilter) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value === undefined || value === null || value === '') continue;
      params = params.set(key, String(value));
    }
    return firstValueFrom(this.http.get<PanelStudentPage>(`${this.ws(id)}/students/search`, { params }));
  }
  lessons(id: string, from: Date, to: Date) {
    const params = new HttpParams().set('from', from.toISOString()).set('to', to.toISOString()).set('take', '500');
    return firstValueFrom(this.http.get<CourseLesson[]>(`${this.ws(id)}/lessons`, { params }));
  }
  completions(id: string, assignmentId: string) {
    return firstValueFrom(this.http.get<PanelCompletion[]>(`${this.ws(id)}/assignments/${assignmentId}/completions`));
  }
  markCompletions(id: string, assignmentId: string, items: { studentId: string; completion: CompletionMark; note: string | null }[]) {
    return firstValueFrom(this.http.put<PanelCompletion[]>(`${this.ws(id)}/assignments/${assignmentId}/completions`, { items }));
  }
  previewSeries(id: string, request: SeriesRequest) {
    return firstValueFrom(this.http.post<SeriesPreview>(`${this.ws(id)}/lesson-series`, { id: crypto.randomUUID(), ...request, preview: true }));
  }
  createSeries(id: string, request: SeriesRequest, skipConflicts: boolean) {
    return firstValueFrom(this.http.post<{ seriesId: string; sessionsCreated: number; sessionsSkipped: number }>(`${this.ws(id)}/lesson-series`,
      { id: crypto.randomUUID(), ...request, skipConflicts }));
  }
  emailSettings(id: string) { return firstValueFrom(this.http.get<PanelEmailSettings>(`${this.ws(id)}/email-settings`)); }
  saveEmailSettings(id: string, settings: Omit<PanelEmailSettings, 'informedConsentAt' | 'rules'> & {
    rules: { kind: number; enabled: boolean; audience: number; note: string | null }[]; informedConsentConfirmed: boolean;
  }) { return firstValueFrom(this.http.put<PanelEmailSettings>(`${this.ws(id)}/email-settings`, settings)); }
  emailPreview(id: string, kind: string) { return firstValueFrom(this.http.get<PanelEmailPreview>(`${this.ws(id)}/email-settings/preview/${kind}`)); }
  sendTestEmail(id: string, kind: number, email: string) {
    return firstValueFrom(this.http.post<unknown>(`${this.ws(id)}/email-settings/test`, { kind, email }));
  }
  sentEmails(id: string, skip: number, take: number, studentId?: string) {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (studentId) params = params.set('studentId', studentId);
    return firstValueFrom(this.http.get<PanelSentEmail[]>(`${this.ws(id)}/emails`, { params }));
  }
  muteStudentEmails(id: string, studentId: string, muted: boolean) {
    return firstValueFrom(this.http.put<{ emailsMuted: boolean }>(`${this.ws(id)}/students/${studentId}/email-mute`, { muted }));
  }
  support(id: string) { return firstValueFrom(this.http.get<PanelSupportView>(`${this.ws(id)}/support-access`)); }
  approveSupport(id: string, grantId: string, hours: number, sections: number[]) {
    return firstValueFrom(this.http.post<PanelSupportGrant>(`${this.ws(id)}/support-access/${grantId}/approve`, { hours, sections }));
  }
  declineSupport(id: string, grantId: string) { return firstValueFrom(this.http.post<PanelSupportGrant>(`${this.ws(id)}/support-access/${grantId}/decline`, {})); }
  endSupport(id: string, grantId: string) { return firstValueFrom(this.http.post<PanelSupportGrant>(`${this.ws(id)}/support-access/${grantId}/end`, {})); }
}
