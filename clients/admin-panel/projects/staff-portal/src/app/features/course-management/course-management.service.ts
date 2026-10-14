import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { collectCourseReportPages } from './course-report-pagination';
import { LegacyInstitutionCategory, LegacyStudentSource, LegacyStudentSourceRow } from './course-management-legacy-import.service';

export {
  buildCourseFinanceSummaryCsv,
  buildCourseStudentAccountCsv,
  buildCourseStudentAccountPrintHtml,
  buildCourseReportCsv,
  buildCourseReportPrintHtml,
  buildCourseStudentRosterReport,
  buildCourseLessonAttendanceReport,
  buildCourseAssignmentProgressReport,
  buildCourseFinanceWorkspaceReport
} from './course-management-reports';

export interface CourseProfile { userId: string; displayName: string; }
export interface CourseWorkspace { id: string; name: string; type: string; institutionCategory?: LegacyInstitutionCategory | null; role: string; isReadOnly: boolean; }
export interface CourseWorkspaceSubscription {
  workspaceId: string; workspaceName: string; workspaceType: string; activeStudentCount: number;
  subscription: { planName: string; activeStudentLimit: number; startsAt: string; endsAt: string; accessStatus: string } | null;
}
export interface CourseMember { userId: string; role: string; isActive: boolean; }
export interface CourseInvitation { id: string; targetUserId: string; studentRecordId: string | null; role: string; status: string; expiresAt: string; }
export interface CourseAuditEntry { action: string; occurredAt: string; }
export interface PendingCourseInvitation { id: string; workspaceName: string; role: string; }
export interface CourseStudent { id: string; userId: string | null; displayName: string; email: string | null; phoneNumber: string | null; isActive: boolean; }
export interface StudentCsvImportRow { rowNumber: number; status: 'Ready' | 'Imported' | 'Invalid' | 'Duplicate'; displayName: string; email: string | null; phoneNumber: string | null; error: string | null; }
export interface StudentCsvImportPreview {
  previewId: string; readyCount: number; importedCount: number; invalidCount: number; duplicateCount: number; rows: StudentCsvImportRow[];
}
export interface StudentCsvImportCommit { previewId: string; createdCount: number; alreadyImportedCount: number; }
export type LegacyStudentImportField = 'displayName' | 'email' | 'isActive';
export interface LegacyStudentImportPreviewRow {
  rowNumber: number; status: 'Ready' | 'Imported' | 'Invalid' | 'Conflict' | 'Duplicate';
  displayName: string; email: string | null; isActive: boolean; error: string | null;
}
export interface LegacyStudentImportPreview {
  previewId: string; sourceSystem: LegacyStudentSource; selectedFields: LegacyStudentImportField[];
  readyCount: number; importedCount: number; invalidCount: number; conflictCount: number; duplicateCount: number;
  rows: LegacyStudentImportPreviewRow[];
}
export interface LegacyStudentImportCommit { previewId: string; createdCount: number; alreadyImportedCount: number; }
export type LegacyInstitutionImportField = 'name' | 'type';
export interface LegacyInstitutionImportRequest {
  sourceSystem: LegacyStudentSource; sourceInstitutionId: string; selectedFields: LegacyInstitutionImportField[];
  name: string | null; institutionCategory: LegacyInstitutionCategory | null;
}
export interface LegacyInstitutionImportPreview {
  previewId: string; sourceSystem: LegacyStudentSource; selectedFields: LegacyInstitutionImportField[];
  workspaceName: string; workspaceInstitutionCategory: LegacyInstitutionCategory | null;
  targetName: string; targetInstitutionCategory: LegacyInstitutionCategory | null;
}
export interface LegacyInstitutionImportCommit {
  previewId: string; name: string; institutionCategory: LegacyInstitutionCategory | null; alreadyApplied: boolean;
}
export interface GuardianContact { id: string; displayName: string; email: string | null; phoneNumber: string | null; }
export interface StudentGuardianLink extends GuardianContact { relationship: string; }
export interface CourseSummary { id: string; name: string; subject: string; }
export interface CourseGroup { id: string; courseId: string; courseName: string; name: string; teacherUserId: string; }
export interface CourseBranch { id: string; name: string; }
export interface CourseRoom { id: string; branchId: string; branchName: string; name: string; capacity: number | null; }
export interface CourseLesson {
  id: string; seriesId: string | null; timeZoneId: string | null; courseId: string; courseName: string; subject: string; workspaceName: string;
  groupId: string | null; groupName: string | null; roomId: string | null; roomName: string | null; branchName: string | null; teacherUserId: string;
  startsAt: string; endsAt: string; status: string;
  students: { studentId: string; displayName: string; attendanceStatus: string | null; isActive?: boolean }[];
}
export interface CourseLessonSeries {
  id: string; courseId: string; courseName: string; groupId: string | null; groupName: string | null;
  roomId: string | null; roomName: string | null; branchName: string | null; teacherUserId: string;
  timeZoneId: string; startsOn: string; endsOn: string; startTime: string; durationMinutes: number;
  daysOfWeekMask: number; nextStartsAt: string; remainingSessions: number;
}
export interface CourseMakeupCredit {
  id: string; studentId: string; studentName: string; sourceLessonId: string; sourceLessonStartsAt: string;
  sourceCourseId: string; sourceTeacherUserId: string; durationMinutes: number; makeupLessonId: string | null;
  reason: string; status: string; grantedAt: string; expiresAt: string;
}
export interface TeacherTimeOff { id: string; teacherUserId: string; startsAt: string; endsAt: string; reason: string | null; createdAt: string; }
export interface TeacherAvailabilityWindow { id: string; teacherUserId: string; weekday: number; startTime: string; endTime: string; timeZoneId: string; }
export interface CourseFeeAgreement {
  id: string; studentId: string; courseId: string; model: string; amount: number; billingUnit: string | null;
  packageSessions: number | null; packageExpiresAt: string | null; isActive: boolean; createdAt: string; closedAt: string | null;
}
export interface CourseInstallment {
  id: string; feeAgreementId: string; studentId: string; sourceLessonId: string | null; reversalOfInstallmentId: string | null;
  label: string; amount: number; remainingAmount: number; dueOn: string; createdAt: string;
}
export interface CoursePayment {
  id: string; studentId: string; reversalOfPaymentId: string | null; method: string; amount: number;
  receivedAt: string; note: string | null; allocatedAmount: number;
}
export interface CoursePackageMovement {
  id: string; feeAgreementId: string; lessonSessionId: string | null; reversalOfMovementId: string | null;
  type: string; quantity: number; occurredAt: string;
}
export interface CourseFinanceSummary {
  from: string; to: string; totalCharges: number; totalPayments: number; balance: number;
  overdueAmount: number; dueInNext30Days: number; collectionsByMethod: { method: string; amount: number }[];
}
export interface CourseFinanceWorkspaceReport {
  generatedOn: string; upcomingThrough: string;
  overdueInstallments: { installmentId: string; feeAgreementId: string; studentId: string; studentName: string;
    courseId: string; courseName: string; label: string; amount: number; remainingAmount: number; dueOn: string }[];
  upcomingInstallments: { installmentId: string; feeAgreementId: string; studentId: string; studentName: string;
    courseId: string; courseName: string; label: string; amount: number; remainingAmount: number; dueOn: string }[];
  packageBalances: { agreementId: string; studentId: string; studentName: string; courseId: string; courseName: string;
    amount: number; grantedSessions: number; expiresAt: string | null; remainingSessions: number }[];
}

export interface CourseStudentAccount {
  studentId: string; balance: number; totalCharges: number; totalPayments: number; unallocatedPaymentAmount: number;
  agreements: CourseFeeAgreement[]; installments: CourseInstallment[]; payments: CoursePayment[];
  packageBalances: { agreementId: string; grantedSessions: number; expiresAt: string | null; remainingSessions: number }[];
  packageMovements: CoursePackageMovement[];
}

export interface CourseAssignment {
  id: string; workspaceId: string; courseId: string; courseName: string | null; groupId: string | null; studentId: string | null;
  teacherUserId: string; title: string; instructions: string | null; dueAt: string; createdAt: string;
  recipientCount: number; submissionCount: number | null;
}
export interface CourseAssignmentSubmission {
  studentId: string; studentName: string; submissionId: string | null; text: string | null; status: string; isLate: boolean;
  submittedAt: string | null; score: number | null; feedback: string | null; reviewedAt: string | null;
  attachments: CourseAssignmentAttachment[];
}
export interface CourseAssignmentAttachment {
  id: string; fileName: string; contentType: string; sizeBytes: number; purpose: string; uploadedAt: string; expiresAt: string;
}

export type CourseReportCell = string | number | null;
export interface CourseReportTable {
  title: string;
  scope: string;
  headers: string[];
  rows: CourseReportCell[][];
}

@Injectable({ providedIn: 'root' })
export class CourseManagementService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/course-management/v1`;
  private attachmentContentType(fileName: string): string {
    const extension = fileName.split('.').at(-1)?.toLowerCase();
    return extension === 'pdf' ? 'application/pdf' : extension === 'jpg' || extension === 'jpeg'
      ? 'image/jpeg' : extension === 'png' ? 'image/png' : 'application/octet-stream';
  }
  getProfile() { return firstValueFrom(this.http.get<CourseProfile>(`${this.base}/me/profile`)); }
  saveProfile(displayName: string) { return firstValueFrom(this.http.put<CourseProfile>(`${this.base}/me/profile`, { displayName })); }
  getWorkspaces() { return firstValueFrom(this.http.get<CourseWorkspace[]>(`${this.base}/me/memberships`)); }
  getMySubscriptions() { return firstValueFrom(this.http.get<CourseWorkspaceSubscription[]>(`${this.base}/me/subscriptions`)); }
  createWorkspace(id: string, name: string, type: 1 | 2) {
    return firstValueFrom(this.http.post<{ id: string }>(`${this.base}/workspaces`, { id, name, type }));
  }
  getMembers(id: string) { return firstValueFrom(this.http.get<CourseMember[]>(`${this.base}/workspaces/${id}/members`)); }
  getInvitations(id: string) { return firstValueFrom(this.http.get<CourseInvitation[]>(`${this.base}/workspaces/${id}/invitations`)); }
  getAudit(id: string) { return firstValueFrom(this.http.get<CourseAuditEntry[]>(`${this.base}/workspaces/${id}/audit`)); }
  invite(workspaceId: string, id: string, targetUserId: string, role: 1 | 2, studentRecordId: string | null = null) {
    return firstValueFrom(this.http.post<CourseInvitation>(`${this.base}/workspaces/${workspaceId}/invitations`, { id, targetUserId, role, studentRecordId }));
  }
  cancel(workspaceId: string, invitationId: string) {
    return firstValueFrom(this.http.post<void>(`${this.base}/workspaces/${workspaceId}/invitations/${invitationId}/cancel`, {}));
  }
  revoke(workspaceId: string, userId: string) {
    return firstValueFrom(this.http.post<void>(`${this.base}/workspaces/${workspaceId}/members/${userId}/revoke`, {}));
  }
  getPendingInvitations() { return firstValueFrom(this.http.get<PendingCourseInvitation[]>(`${this.base}/participation/invitations`)); }
  getStudents(workspaceId: string) { return this.getStudentRosterForReport(workspaceId); }
  getStudentRosterForReport(workspaceId: string) {
    return this.getAllPages<CourseStudent>(`/workspaces/${workspaceId}/students`, 200);
  }
  addStudent(workspaceId: string, displayName: string, email: string, phoneNumber: string) {
    return firstValueFrom(this.http.post<CourseStudent>(`${this.base}/workspaces/${workspaceId}/students`, {
      id: crypto.randomUUID(), displayName, email: email.trim() || null, phoneNumber: phoneNumber.trim() || null
    }));
  }
  previewStudentCsv(workspaceId: string, csv: string) {
    return firstValueFrom(this.http.post<StudentCsvImportPreview>(`${this.base}/workspaces/${workspaceId}/imports/students/preview`, { csv }));
  }
  commitStudentCsv(workspaceId: string, previewId: string, csv: string) {
    return firstValueFrom(this.http.post<StudentCsvImportCommit>(`${this.base}/workspaces/${workspaceId}/imports/students`, { previewId, csv }));
  }
  previewLegacyStudents(workspaceId: string, sourceSystem: LegacyStudentSource, selectedFields: LegacyStudentImportField[], rows: LegacyStudentSourceRow[]) {
    return firstValueFrom(this.http.post<LegacyStudentImportPreview>(
      `${this.base}/workspaces/${workspaceId}/imports/legacy-students/preview`, { sourceSystem, selectedFields, rows }));
  }
  commitLegacyStudents(workspaceId: string, previewId: string, sourceSystem: LegacyStudentSource,
    selectedFields: LegacyStudentImportField[], rows: LegacyStudentSourceRow[]) {
    return firstValueFrom(this.http.post<LegacyStudentImportCommit>(
      `${this.base}/workspaces/${workspaceId}/imports/legacy-students`, { previewId, sourceSystem, selectedFields, rows }));
  }
  previewLegacyInstitution(workspaceId: string, request: LegacyInstitutionImportRequest) {
    return firstValueFrom(this.http.post<LegacyInstitutionImportPreview>(
      `${this.base}/workspaces/${workspaceId}/imports/legacy-institution/preview`, request));
  }
  commitLegacyInstitution(workspaceId: string, request: LegacyInstitutionImportRequest, preview: LegacyInstitutionImportPreview) {
    return firstValueFrom(this.http.post<LegacyInstitutionImportCommit>(
      `${this.base}/workspaces/${workspaceId}/imports/legacy-institution`, {
        ...request, previewId: preview.previewId, expectedWorkspaceName: preview.workspaceName,
        expectedWorkspaceCategory: preview.workspaceInstitutionCategory
      }));
  }
  updateStudent(workspaceId: string, studentId: string, displayName: string, email: string, phoneNumber: string, isActive: boolean) {
    return firstValueFrom(this.http.put<CourseStudent>(`${this.base}/workspaces/${workspaceId}/students/${studentId}`, {
      displayName, email: email.trim() || null, phoneNumber: phoneNumber.trim() || null, isActive
    }));
  }
  getGuardians(workspaceId: string) { return firstValueFrom(this.http.get<GuardianContact[]>(`${this.base}/workspaces/${workspaceId}/guardians`)); }
  updateGuardian(workspaceId: string, guardianId: string, displayName: string, email: string, phoneNumber: string) {
    return firstValueFrom(this.http.put<GuardianContact>(`${this.base}/workspaces/${workspaceId}/guardians/${guardianId}`, {
      displayName, email: email.trim() || null, phoneNumber: phoneNumber.trim() || null
    }));
  }
  getStudentGuardians(workspaceId: string, studentId: string) {
    return firstValueFrom(this.http.get<StudentGuardianLink[]>(`${this.base}/workspaces/${workspaceId}/students/${studentId}/guardians`));
  }
  linkGuardian(workspaceId: string, studentId: string, guardianId: string, relationship: number,
    contact?: { displayName: string; email: string; phoneNumber: string }) {
    return firstValueFrom(this.http.post<StudentGuardianLink>(`${this.base}/workspaces/${workspaceId}/students/${studentId}/guardians`, {
      guardianId, relationship, displayName: contact?.displayName ?? null,
      email: contact?.email.trim() || null, phoneNumber: contact?.phoneNumber.trim() || null
    }));
  }
  unlinkGuardian(workspaceId: string, studentId: string, guardianId: string) {
    return firstValueFrom(this.http.delete<void>(`${this.base}/workspaces/${workspaceId}/students/${studentId}/guardians/${guardianId}`));
  }
  getCourses(workspaceId: string) { return firstValueFrom(this.http.get<CourseSummary[]>(`${this.base}/workspaces/${workspaceId}/courses`)); }
  createCourse(workspaceId: string, name: string, subject: string) {
    return firstValueFrom(this.http.post<CourseSummary>(`${this.base}/workspaces/${workspaceId}/courses`, { id: crypto.randomUUID(), name, subject }));
  }
  getBranches(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseBranch[]>(`${this.base}/workspaces/${workspaceId}/branches`));
  }
  createBranch(workspaceId: string, name: string) {
    return firstValueFrom(this.http.post<CourseBranch>(`${this.base}/workspaces/${workspaceId}/branches`, { id: crypto.randomUUID(), name }));
  }
  getRooms(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseRoom[]>(`${this.base}/workspaces/${workspaceId}/rooms`));
  }
  createRoom(workspaceId: string, branchId: string, name: string, capacity: number | null) {
    return firstValueFrom(this.http.post<CourseRoom>(`${this.base}/workspaces/${workspaceId}/branches/${branchId}/rooms`, {
      id: crypto.randomUUID(), name, capacity
    }));
  }
  getGroups(workspaceId: string) { return firstValueFrom(this.http.get<CourseGroup[]>(`${this.base}/workspaces/${workspaceId}/groups`)); }
  createGroup(workspaceId: string, courseId: string, teacherUserId: string, name: string) {
    return firstValueFrom(this.http.post<CourseGroup>(`${this.base}/workspaces/${workspaceId}/groups`, {
      id: crypto.randomUUID(), courseId, teacherUserId, name
    }));
  }
  enrollStudent(workspaceId: string, groupId: string, studentId: string) {
    return firstValueFrom(this.http.post<unknown>(`${this.base}/workspaces/${workspaceId}/groups/${groupId}/students/${studentId}`, {}));
  }
  getLessons(workspaceId: string) {
    return this.getLessonAttendanceForReport(workspaceId);
  }
  getLessonAttendanceForReport(workspaceId: string) {
    const from = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString();
    const to = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();
    return this.getAllPages<CourseLesson>(`/workspaces/${workspaceId}/lessons`, 500, { from, to });
  }
  getMakeupCredits(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseMakeupCredit[]>(`${this.base}/workspaces/${workspaceId}/makeup-credits`));
  }
  approveMakeupCredit(workspaceId: string, creditId: string) {
    return firstValueFrom(this.http.post<{ id: string; status: string; expiresAt: string }>(
      `${this.base}/workspaces/${workspaceId}/makeup-credits/${creditId}/approve`, {}));
  }
  getLessonSeries(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseLessonSeries[]>(`${this.base}/workspaces/${workspaceId}/lesson-series`));
  }
  getTeacherTimeOff(workspaceId: string) {
    const from = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString();
    const to = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString();
    return firstValueFrom(this.http.get<TeacherTimeOff[]>(`${this.base}/workspaces/${workspaceId}/teacher-time-off`, { params: { from, to } }));
  }
  createTeacherTimeOff(workspaceId: string, teacherUserId: string, startsAt: string, endsAt: string, reason: string) {
    return firstValueFrom(this.http.post<TeacherTimeOff>(`${this.base}/workspaces/${workspaceId}/teacher-time-off`, {
      id: crypto.randomUUID(), teacherUserId, startsAt, endsAt, reason: reason.trim() || null
    }));
  }
  deleteTeacherTimeOff(workspaceId: string, blockId: string) {
    return firstValueFrom(this.http.delete<void>(`${this.base}/workspaces/${workspaceId}/teacher-time-off/${blockId}`));
  }
  getTeacherAvailability(workspaceId: string) {
    return firstValueFrom(this.http.get<TeacherAvailabilityWindow[]>(`${this.base}/workspaces/${workspaceId}/teacher-availability`));
  }
  createTeacherAvailability(workspaceId: string, teacherUserId: string, weekday: number, startTime: string,
    endTime: string, timeZoneId: string) {
    return firstValueFrom(this.http.post<TeacherAvailabilityWindow>(`${this.base}/workspaces/${workspaceId}/teacher-availability`, {
      id: crypto.randomUUID(), teacherUserId, weekday, startTime, endTime, timeZoneId: timeZoneId.trim()
    }));
  }
  deleteTeacherAvailability(workspaceId: string, windowId: string) {
    return firstValueFrom(this.http.delete<void>(`${this.base}/workspaces/${workspaceId}/teacher-availability/${windowId}`));
  }
  getStudentAccount(workspaceId: string, studentId: string) {
    return firstValueFrom(this.http.get<CourseStudentAccount>(`${this.base}/workspaces/${workspaceId}/finance/students/${studentId}/account`));
  }
  getAssignments(workspaceId: string) { return this.getAssignmentProgressForReport(workspaceId); }
  getAssignmentProgressForReport(workspaceId: string) {
    return this.getAllPages<CourseAssignment>(`/workspaces/${workspaceId}/assignments`, 200);
  }
  createAssignment(workspaceId: string, assignment: {
    id: string; courseId: string; groupId: string | null; studentId: string | null;
    title: string; instructions: string | null; dueAt: string;
  }) {
    return firstValueFrom(this.http.post<CourseAssignment>(`${this.base}/workspaces/${workspaceId}/assignments`, assignment));
  }
  getAssignmentSubmissions(workspaceId: string, assignmentId: string) {
    return firstValueFrom(this.http.get<CourseAssignmentSubmission[]>(
      `${this.base}/workspaces/${workspaceId}/assignments/${assignmentId}/submissions`));
  }
  getAssignmentAttachments(workspaceId: string, assignmentId: string) {
    return firstValueFrom(this.http.get<CourseAssignmentAttachment[]>(
      `${this.base}/workspaces/${workspaceId}/assignments/${assignmentId}/attachments`));
  }
  uploadAssignmentAttachment(workspaceId: string, assignmentId: string, file: File) {
    const contentType = this.attachmentContentType(file.name);
    return firstValueFrom(this.http.post<CourseAssignmentAttachment>(
      `${this.base}/workspaces/${workspaceId}/assignments/${assignmentId}/attachments?fileName=${encodeURIComponent(file.name)}`,
      new Blob([file], { type: contentType }), { headers: { 'Content-Type': contentType } }));
  }
  downloadAssignmentAttachment(workspaceId: string, assignmentId: string, attachmentId: string) {
    return firstValueFrom(this.http.get(
      `${this.base}/workspaces/${workspaceId}/assignments/${assignmentId}/attachments/${attachmentId}/content`,
      { responseType: 'blob' }));
  }
  reviewAssignment(workspaceId: string, assignmentId: string, studentId: string, score: number | null, feedback: string) {
    return firstValueFrom(this.http.put<CourseAssignmentSubmission>(
      `${this.base}/workspaces/${workspaceId}/assignments/${assignmentId}/submissions/${studentId}/grade`, { score, feedback }));
  }
  getFinanceSummary(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseFinanceSummary>(`${this.base}/workspaces/${workspaceId}/finance/summary`));
  }
  getFinanceWorkspaceReport(workspaceId: string) {
    return firstValueFrom(this.http.get<CourseFinanceWorkspaceReport>(`${this.base}/workspaces/${workspaceId}/finance/report`));
  }
  createFeeAgreement(workspaceId: string, agreement: {
    id: string; studentId: string; courseId: string; model: 1 | 2 | 3 | 4; amount: number;
    billingUnit?: 1 | 2 | null; packageSessions?: number | null; packageExpiresAt?: string | null;
  }) {
    return firstValueFrom(this.http.post<CourseFeeAgreement>(`${this.base}/workspaces/${workspaceId}/finance/fee-agreements`, agreement));
  }
  closeFeeAgreement(workspaceId: string, agreementId: string) {
    return firstValueFrom(this.http.post<CourseFeeAgreement>(
      `${this.base}/workspaces/${workspaceId}/finance/fee-agreements/${agreementId}/close`, {}));
  }
  createInstallment(workspaceId: string, agreementId: string, installment: { id: string; label: string; amount: number; dueOn: string }) {
    return firstValueFrom(this.http.post<CourseInstallment>(
      `${this.base}/workspaces/${workspaceId}/finance/fee-agreements/${agreementId}/installments`, installment));
  }
  recordPayment(workspaceId: string, payment: {
    id: string; studentId: string; method: 1 | 2; amount: number; receivedAt: string; note: string | null;
    allocations: { installmentId: string; amount: number }[];
  }) {
    return firstValueFrom(this.http.post<CoursePayment>(`${this.base}/workspaces/${workspaceId}/finance/payments`, payment));
  }
  reversePayment(workspaceId: string, paymentId: string, id: string, receivedAt: string, reason: string) {
    return firstValueFrom(this.http.post<CoursePayment>(
      `${this.base}/workspaces/${workspaceId}/finance/payments/${paymentId}/reverse`, { id, receivedAt, reason }));
  }
  reverseInstallment(workspaceId: string, installmentId: string, id: string) {
    return firstValueFrom(this.http.post<CourseInstallment>(
      `${this.base}/workspaces/${workspaceId}/finance/installments/${installmentId}/reverse`, { id }));
  }
  reversePackageMovement(workspaceId: string, movementId: string, id: string) {
    return firstValueFrom(this.http.post<CoursePackageMovement>(
      `${this.base}/workspaces/${workspaceId}/finance/package-movements/${movementId}/reverse`, { id }));
  }
  createLesson(workspaceId: string, courseId: string, groupId: string | null, studentId: string | null,
    teacherUserId: string, startsAt: string, endsAt: string, roomId: string | null = null, makeupCreditId: string | null = null) {
    return firstValueFrom(this.http.post<unknown>(`${this.base}/workspaces/${workspaceId}/lessons`, {
      id: crypto.randomUUID(), courseId, groupId, studentId, teacherUserId, startsAt, endsAt, roomId, makeupCreditId
    }));
  }
  rescheduleLesson(workspaceId: string, lessonId: string, startsAt: string, endsAt: string, roomId: string | null) {
    return firstValueFrom(this.http.put<{ id: string; startsAt: string; endsAt: string; roomId: string | null }>(
      `${this.base}/workspaces/${workspaceId}/lessons/${lessonId}/schedule`, { startsAt, endsAt, roomId }));
  }
  createLessonSeries(workspaceId: string, courseId: string, groupId: string | null, studentId: string | null,
    teacherUserId: string, timeZoneId: string, startsOn: string, endsOn: string, startTime: string,
    durationMinutes: number, daysOfWeekMask: number, roomId: string | null = null) {
    return firstValueFrom(this.http.post<{ seriesId: string; sessionsCreated: number }>(`${this.base}/workspaces/${workspaceId}/lesson-series`, {
      id: crypto.randomUUID(), courseId, groupId, studentId, teacherUserId,
      timeZoneId, startsOn, endsOn, startTime, durationMinutes, daysOfWeekMask, roomId
    }));
  }
  cancelLesson(workspaceId: string, lessonId: string) {
    return firstValueFrom(this.http.post<void>(`${this.base}/workspaces/${workspaceId}/lessons/${lessonId}/cancel`, {}));
  }
  cancelFutureLessonSeries(workspaceId: string, seriesId: string) {
    return firstValueFrom(this.http.post<{ seriesId: string; sessionsCancelled: number }>(
      `${this.base}/workspaces/${workspaceId}/lesson-series/${seriesId}/cancel-future`, {}));
  }
  updateLessonSeriesSchedule(workspaceId: string, seriesId: string, endsOn: string, startTime: string,
    durationMinutes: number, daysOfWeekMask: number, roomId: string | null) {
    return firstValueFrom(this.http.put<{ seriesId: string; sessionsCreated: number; sessionsUpdated: number; sessionsCancelled: number }>(
      `${this.base}/workspaces/${workspaceId}/lesson-series/${seriesId}/schedule`, {
        endsOn, startTime, durationMinutes, daysOfWeekMask, roomId
      }));
  }
  markAttendance(workspaceId: string, lessonId: string, studentId: string, status: number) {
    return firstValueFrom(this.http.put<unknown>(`${this.base}/workspaces/${workspaceId}/lessons/${lessonId}/students/${studentId}/attendance`, { status }));
  }
  completeLesson(workspaceId: string, lessonId: string) {
    return firstValueFrom(this.http.post<void>(`${this.base}/workspaces/${workspaceId}/lessons/${lessonId}/complete`, {}));
  }
  accept(invitationId: string, displayName: string) {
    return firstValueFrom(this.http.post<unknown>(`${this.base}/participation/invitations/${invitationId}/accept`, { displayName }));
  }
  private async getAllPages<T>(path: string, pageSize: number, filters: Record<string, string> = {}): Promise<T[]> {
    return collectCourseReportPages(pageSize, (skip, take) => firstValueFrom(this.http.get<T[]>(`${this.base}${path}`, {
      params: { ...filters, skip, take }
    })));
  }
}
