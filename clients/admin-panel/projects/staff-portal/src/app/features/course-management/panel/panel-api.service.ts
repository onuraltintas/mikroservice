import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CourseLesson } from '../course-management.service';

/** Endpoints the institution panel (19.11.2026) adds on top of CourseManagementService. */
/** A teacher invited by e-mail address (maxkampus.com/davet). */
export interface PanelTeacherInvitation {
  id: string; email: string; displayName: string | null; status: 'Pending' | 'Accepted' | 'Cancelled' | 'Expired'; createdAt: string; expiresAt: string; acceptedAt: string | null;
}
/** Setup guide (20.11.2026): each step's state, read from the data on the server. */
export type SetupStepId = 'profile' | 'teachers' | 'rooms' | 'courses' | 'students' | 'fees' | 'schedule' | 'emails';
export interface PanelSetup {
  name: string; institutionCategory: 'School' | 'PrivateCourse' | 'StudyCenter' | 'OnlinePlatform' | null; closed: boolean;
  steps: { id: SetupStepId; status: 'done' | 'skipped' | 'open' }[];
  counts: { teachers: number; invited: number; rooms: number; courses: number; groups: number; students: number; unplaced: number; withoutFee: number; futureLessons: number };
}
export interface PanelMember { userId: string; role: 'Owner' | 'Teacher' | 'Student'; isActive: boolean; displayName: string | null; invited?: boolean; email?: string; }
export interface PanelOverviewLesson {
  id: string; courseName: string; groupName: string | null; roomName: string | null; teacherName: string | null; teacherUserId: string;
  startsAt: string; endsAt: string; studentCount: number; attendanceTaken: boolean; status: 'Scheduled' | 'InProgress' | 'Cancelled' | 'Completed';
}
export interface PanelOverview {
  today: string; role: 'Owner' | 'Teacher'; todayLessons: PanelOverviewLesson[];
  lessonsWaitingForAttendance: { id: string; courseName: string; startsAt: string; groupName: string | null }[];
  finance: { overdueCount: number; overdueStudentCount: number; overdueAmount: number; monthCollections: number } | null;
  emails: { sent: number; skipped: number; byKind: { kind: string; count: number }[] } | null;
}
export interface PanelStudentRow {
  id: string; displayName: string; email: string | null; phoneNumber: string | null; isActive: boolean; emailsMuted: boolean;
  createdAt: string; oneToOneTeacherUserId?: string | null; groups: { id: string; name: string; courseName: string }[]; guardianCount: number;
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
  delivery: 'Unknown' | 'Delivered' | 'PartlyFailed' | 'Failed'; failedRecipientCount: number;
}
export interface PanelSupportLog {
  section: string; what: string; occurredAt: string; isChange: boolean; target: string | null; before: string | null; after: string | null;
}
export interface PanelSupportView { logs: PanelSupportLog[]; }
export interface SeriesRequest {
  courseId: string; groupId: string | null; studentId: string | null; teacherUserId: string; startsOn: string; endsOn: string;
  startTime: string; durationMinutes: number; daysOfWeekMask: number; roomId: string | null;
  repeatEveryWeeks: number; monthlyWeekOfMonth: number | null;
}
export interface SeriesPreview { sessionCount: number; conflictCount: number; occurrences: { startsAt: string; endsAt: string; conflicts: string[] }[]; }

export interface PanelInstallmentRow {
  id: string; studentId: string; studentName: string; courseId: string; courseName: string; label: string; amount: number;
  remaining: number; dueOn: string; isLessonCharge: boolean; overdueDays: number;
}
export interface PanelPaymentRow {
  id: string; studentId: string; studentName: string; amount: number; receivedAt: string; method: 'Cash' | 'BankTransfer';
  note: string | null; isReversal: boolean; reversed: boolean;
}

@Injectable({ providedIn: 'root' })
export class CoursePanelApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/course-management/v1`;
  private ws(id: string) { return `${this.base}/workspaces/${id}`; }

  overview(id: string) { return firstValueFrom(this.http.get<PanelOverview>(`${this.ws(id)}/overview`)); }
  members(id: string) { return firstValueFrom(this.http.get<PanelMember[]>(`${this.ws(id)}/members`)); }
  /** maxkampüs (19.11.2026): the address needs no account; the teacher gets a link and opens one if needed. */
  inviteTeacher(id: string, email: string, displayName: string | null = null) {
    return firstValueFrom(this.http.post<PanelTeacherInvitation>(`${this.ws(id)}/teacher-email-invitations`, { id: crypto.randomUUID(), email, displayName }));
  }
  // Rooms and branches (21.11.2026): correct, archive, delete. A 409 with `lessons` asks the owner before `force`.
  updateBranch(id: string, branchId: string, name: string) { return firstValueFrom(this.http.put<void>(`${this.ws(id)}/branches/${branchId}`, { name })); }
  archiveBranch(id: string, branchId: string) { return firstValueFrom(this.http.post<void>(`${this.ws(id)}/branches/${branchId}/archive`, {})); }
  restoreBranch(id: string, branchId: string) { return firstValueFrom(this.http.post<void>(`${this.ws(id)}/branches/${branchId}/restore`, {})); }
  deleteBranch(id: string, branchId: string) { return firstValueFrom(this.http.delete<void>(`${this.ws(id)}/branches/${branchId}`)); }
  updateRoom(id: string, roomId: string, name: string, capacity: number | null, force = false) {
    return firstValueFrom(this.http.put<void>(`${this.ws(id)}/rooms/${roomId}`, { name, capacity, force }));
  }
  archiveRoom(id: string, roomId: string, force = false) {
    return firstValueFrom(this.http.post<void>(`${this.ws(id)}/rooms/${roomId}/archive${force ? '?force=true' : ''}`, {}));
  }
  restoreRoom(id: string, roomId: string) { return firstValueFrom(this.http.post<void>(`${this.ws(id)}/rooms/${roomId}/restore`, {})); }
  deleteRoom(id: string, roomId: string, force = false) {
    return firstValueFrom(this.http.delete<{ freedLessons: number }>(`${this.ws(id)}/rooms/${roomId}${force ? '?force=true' : ''}`));
  }
  feeAgreements(id: string) {
    return firstValueFrom(this.http.get<{ id: string; studentId: string; courseId: string; model: string; isActive: boolean }[]>(`${this.ws(id)}/finance/fee-agreements`));
  }
  setup(id: string) { return firstValueFrom(this.http.get<PanelSetup>(`${this.ws(id)}/setup`)); }
  saveSetup(id: string, skipped: SetupStepId[], closed: boolean) { return firstValueFrom(this.http.put<void>(`${this.ws(id)}/setup`, { skipped, closed })); }
  saveProfile(id: string, name: string, institutionCategory: number | null) {
    return firstValueFrom(this.http.put<{ name: string; institutionCategory: string | null }>(`${this.ws(id)}/profile`, { name, institutionCategory }));
  }
  updateGroup(id: string, groupId: string, name: string, teacherUserId: string | null, moveFutureLessons: boolean, force = false) {
    return firstValueFrom(this.http.put<{ id: string; name: string; teacherUserId: string; movedLessons: number }>(`${this.ws(id)}/groups/${groupId}`,
      { name, teacherUserId, moveFutureLessons, force }));
  }
  removeFromGroup(id: string, groupId: string, studentId: string) { return firstValueFrom(this.http.delete<void>(`${this.ws(id)}/groups/${groupId}/students/${studentId}`)); }
  setOneToOneTeacher(id: string, studentId: string, teacherUserId: string | null) {
    return firstValueFrom(this.http.put<{ id: string; oneToOneTeacherUserId: string | null }>(`${this.ws(id)}/students/${studentId}/one-to-one-teacher`, { teacherUserId }));
  }
  teacherInvitations(id: string) { return firstValueFrom(this.http.get<PanelTeacherInvitation[]>(`${this.ws(id)}/teacher-email-invitations`)); }
  cancelTeacherInvitation(id: string, invitationId: string) {
    return firstValueFrom(this.http.post<void>(`${this.ws(id)}/teacher-email-invitations/${invitationId}/cancel`, {}));
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
  installments(id: string, filter: { status: string; courseId?: string; q?: string; skip: number; take: number }) {
    let params = new HttpParams().set('status', filter.status).set('skip', filter.skip).set('take', filter.take);
    if (filter.courseId) params = params.set('courseId', filter.courseId);
    if (filter.q?.trim()) params = params.set('q', filter.q.trim());
    return firstValueFrom(this.http.get<{ total: number; outstanding: number; skip: number; take: number; items: PanelInstallmentRow[] }>(
      `${this.ws(id)}/finance/installments`, { params }));
  }
  payments(id: string, filter: { from?: string; to?: string; method?: string; q?: string; skip: number; take: number }) {
    let params = new HttpParams().set('skip', filter.skip).set('take', filter.take);
    for (const key of ['from', 'to', 'method', 'q'] as const) if (filter[key]) params = params.set(key, String(filter[key]).trim());
    return firstValueFrom(this.http.get<{ total: number; sum: number; skip: number; take: number; items: PanelPaymentRow[] }>(
      `${this.ws(id)}/finance/payments`, { params }));
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
}
