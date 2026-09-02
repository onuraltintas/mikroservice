import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface CoachingAdminAssignment {
  id: string;
  teacherId: string;
  institutionId?: string;
  title: string;
  status: string;
  dueDate: string;
  studentCount: number;
  submittedStudentCount: number;
  createdAt: string;
}

export interface CoachingAdminOverview {
  totalAssignments: number;
  activeAssignments: number;
  completedAssignments: number;
  cancelledAssignments: number;
  totalAssignmentStudents: number;
  submittedAssignmentStudents: number;
  totalExams: number;
  totalExamResults: number;
  totalSessions: number;
  upcomingSessions: number;
  totalGoals: number;
  completedGoals: number;
  recentAssignments: CoachingAdminAssignment[];
}

export interface InstitutionCoachingComparison {
  institutionId: string;
  gradeLevel?: number;
  fromDate: string;
  toDate: string;
  studentCount: number;
  assignmentCount: number;
  assignedAssignmentCount: number;
  submittedAssignmentCount: number;
  gradedAssignmentCount: number;
  averageAssignmentPercentage?: number;
  examCount: number;
  examResultCount: number;
  averageExamPercentage?: number;
  sessionCount: number;
  attendanceRecordedCount: number;
  attendedSessionCount: number;
  attendancePercentage?: number;
  goalCount: number;
  completedGoalCount: number;
  averageGoalProgress: number;
}

export interface StudentEarlyWarning {
  studentId: string;
  riskLevel: 'Low' | 'Medium' | 'High' | number;
  riskScore: number;
  reasonCodes: string[];
  assignmentCount: number;
  submittedAssignmentCount: number;
  averageAssignmentPercentage?: number;
  recordedAttendanceCount: number;
  attendedSessionCount: number;
  attendancePercentage?: number;
  goalCount: number;
  averageGoalProgress: number;
  lastActivityAt?: string;
}

export interface InstitutionEarlyWarningReport {
  institutionId: string;
  gradeLevel?: number;
  fromDate: string;
  toDate: string;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: StudentEarlyWarning[];
}

export interface CoachingAdminAssignmentListItem extends CoachingAdminAssignment {
  source: string;
  bookTitle?: string;
  bookStartPage?: number;
  bookEndPage?: number;
  attachmentCount: number;
}

export interface CoachingAdminAssignmentPage {
  items: CoachingAdminAssignmentListItem[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CoachingAdminAssignmentAttachment {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  uploadedAt?: string;
  scannedAt?: string;
}

export interface CoachingAdminAssignmentStudent {
  studentId: string;
  submittedAt?: string;
  score?: number;
  teacherFeedback?: string;
  status: string;
  attachments?: CoachingAdminAssignmentAttachment[];
}

export interface CoachingAdminAssignmentDetail {
  id: string;
  teacherId: string;
  institutionId?: string;
  title: string;
  description?: string;
  subject?: string;
  type: string;
  source: string;
  bookTitle?: string;
  bookIsbn?: string;
  bookEdition?: string;
  bookChapter?: string;
  bookStartPage?: number;
  bookEndPage?: number;
  bookStartQuestion?: number;
  bookEndQuestion?: number;
  targetGradeLevel?: number;
  dueDate: string;
  estimatedDurationMinutes?: number;
  maxScore?: number;
  passingScore?: number;
  status: string;
  assignedStudents: CoachingAdminAssignmentStudent[];
  createdAt: string;
}

export interface CoachingAdminSessionListItem {
  id: string;
  teacherId: string;
  institutionId?: string;
  title: string;
  sessionType: string;
  scheduledDate: string;
  durationMinutes: number;
  status: string;
  studentCount: number;
  presentCount: number;
  createdAt: string;
}

export interface CoachingAdminSessionAttendance {
  studentId: string;
  status: string;
  teacherNote?: string;
}

export interface CoachingAdminSessionDetail {
  id: string;
  teacherId: string;
  institutionId?: string;
  title: string;
  sessionType: string;
  scheduledDate: string;
  durationMinutes: number;
  status: string;
  attendances: CoachingAdminSessionAttendance[];
  meetingLink?: string;
  description?: string;
  teacherNotes?: string;
}

export interface CoachingAdminExamListItem {
  id: string;
  createdByTeacherId: string;
  institutionId?: string;
  title: string;
  examType: string;
  examDate: string;
  maxScore: number;
  resultCount: number;
  createdAt: string;
}

export interface CoachingAdminExamResult {
  id: string;
  studentId: string;
  score: number;
  correctAnswers?: number;
  wrongAnswers?: number;
  emptyAnswers?: number;
  subjectScores?: Record<string, number>;
  ranking?: number;
  teacherNotes?: string;
}

export interface CoachingAdminExamDetail {
  id: string;
  createdByTeacherId: string;
  institutionId?: string;
  title: string;
  examType: string;
  subject?: string;
  examDate: string;
  durationMinutes?: number;
  maxScore: number;
  targetGradeLevel?: number;
  description?: string;
  results: CoachingAdminExamResult[];
}

export interface CoachingAdminGoalDetail {
  id: string;
  studentId: string;
  setByTeacherId?: string;
  title: string;
  description?: string;
  category: string;
  targetExamType?: string;
  targetSubject?: string;
  targetScore?: number;
  targetDate?: string;
  currentProgress: number;
  isCompleted: boolean;
  createdAt: string;
}

export interface CoachingAdminGoalListItem {
  id: string;
  studentId: string;
  setByTeacherId?: string;
  title: string;
  category: string;
  targetDate?: string;
  currentProgress: number;
  isCompleted: boolean;
  createdAt: string;
}

export interface CoachingAdminSessionPage { items: CoachingAdminSessionListItem[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number; }
export interface CoachingAdminExamPage { items: CoachingAdminExamListItem[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number; }
export interface CoachingAdminGoalPage { items: CoachingAdminGoalListItem[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number; }

export interface CoachingAdminAssignmentCreateRequest {
  teacherId: string;
  institutionId?: string;
  title: string;
  description?: string;
  subject?: string;
  assignmentType: string;
  assignmentSource: string;
  targetGradeLevel?: number;
  bookTitle?: string;
  bookIsbn?: string;
  bookEdition?: string;
  bookChapter?: string;
  bookStartPage?: number;
  bookEndPage?: number;
  bookStartQuestion?: number;
  bookEndQuestion?: number;
  dueDate: string;
  estimatedDurationMinutes?: number;
  maxScore?: number;
  passingScore?: number;
  studentIds: string[];
}

export interface CoachingAdminSessionCreateRequest {
  teacherId: string;
  studentId: string;
  startTime: string;
  durationMinutes: number;
  subject?: string;
  notes?: string;
  meetingLink?: string;
  type: string;
  studentIds?: string[];
}

export interface CoachingAdminExamCreateRequest {
  teacherId: string;
  title: string;
  type: string;
  examDate: string;
  maxScore: number;
  institutionId?: string;
  description?: string;
}

export interface CoachingAdminGoalCreateRequest {
  studentId: string;
  title: string;
  category: string;
  teacherId?: string;
  description?: string;
  targetDate?: string;
  targetScore?: number;
}

export interface CoachingAdminAssignmentUpdateRequest {
  assignmentId: string;
  title: string;
  description?: string | null;
  subject?: string | null;
  assignmentSource: string;
  targetGradeLevel?: number | null;
  bookTitle?: string | null;
  bookIsbn?: string | null;
  bookEdition?: string | null;
  bookChapter?: string | null;
  bookStartPage?: number | null;
  bookEndPage?: number | null;
  bookStartQuestion?: number | null;
  bookEndQuestion?: number | null;
  dueDate: string;
  estimatedDurationMinutes?: number | null;
  maxScore?: number | null;
  passingScore?: number | null;
  studentIds?: string[] | null;
}

export interface CoachingAdminSessionUpdateRequest {
  sessionId: string;
  title: string;
  description?: string | null;
  scheduledDate: string;
  durationMinutes: number;
  meetingLink?: string | null;
  teacherNotes?: string | null;
}

export interface CoachingAdminExamUpdateRequest {
  examId: string;
  title: string;
  type: string;
  subject?: string | null;
  description?: string | null;
  examDate: string;
  durationMinutes?: number | null;
  maxScore: number;
  targetGradeLevel?: number | null;
}

export interface CoachingAdminExamResultUpdateRequest {
  examId: string;
  resultId: string;
  score: number;
  correctAnswers: number;
  wrongAnswers: number;
  emptyAnswers: number;
  subjectScores?: Record<string, number> | null;
  ranking?: number | null;
  notes?: string | null;
}

export interface CoachingAdminGoalUpdateRequest {
  goalId: string;
  title: string;
  description?: string | null;
  category: string;
  targetDate?: string | null;
  targetScore?: number | null;
  targetExamType?: string | null;
  targetSubject?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching-admin`;

  getOverview(recentLimit = 10) {
    const params = new HttpParams().set('recentLimit', recentLimit);
    return this.http.get<CoachingAdminOverview>(`${this.url}/overview`, { params });
  }

  getInstitutionComparison(
    institutionId: string,
    options: { gradeLevel?: number; fromDate?: string; toDate?: string } = {}
  ) {
    let params = new HttpParams();
    if (options.gradeLevel !== undefined && options.gradeLevel !== null) {
      params = params.set('gradeLevel', options.gradeLevel);
    }
    if (options.fromDate) params = params.set('fromDate', options.fromDate);
    if (options.toDate) params = params.set('toDate', options.toDate);
    return this.http.get<InstitutionCoachingComparison>(
      `${environment.apiUrl}/reports/institution/${encodeURIComponent(institutionId)}/comparison`,
      { params }
    );
  }

  getInstitutionEarlyWarnings(
    institutionId: string,
    options: {
      pageNumber?: number;
      pageSize?: number;
      gradeLevel?: number;
      fromDate?: string;
      toDate?: string;
    } = {}
  ) {
    let params = new HttpParams()
      .set('pageNumber', options.pageNumber ?? 1)
      .set('pageSize', options.pageSize ?? 25);
    if (options.gradeLevel !== undefined && options.gradeLevel !== null) {
      params = params.set('gradeLevel', options.gradeLevel);
    }
    if (options.fromDate) params = params.set('fromDate', options.fromDate);
    if (options.toDate) params = params.set('toDate', options.toDate);
    return this.http.get<InstitutionEarlyWarningReport>(
      `${environment.apiUrl}/reports/institution/${encodeURIComponent(institutionId)}/early-warnings`,
      { params }
    );
  }

  getAssignments(options: {
    pageNumber?: number;
    pageSize?: number;
    status?: string;
    source?: string;
    search?: string;
  } = {}) {
    let params = new HttpParams()
      .set('pageNumber', options.pageNumber ?? 1)
      .set('pageSize', options.pageSize ?? 25);
    if (options.status) params = params.set('status', options.status);
    if (options.source) params = params.set('source', options.source);
    if (options.search?.trim()) params = params.set('search', options.search.trim());
    return this.http.get<CoachingAdminAssignmentPage>(`${this.url}/assignments`, { params });
  }

  getAssignment(id: string) {
    return this.http.get<CoachingAdminAssignmentDetail>(`${this.url}/assignments/${encodeURIComponent(id)}`);
  }

  createAssignment(request: CoachingAdminAssignmentCreateRequest, idempotencyKey: string) {
    return this.http.post<{ assignmentId: string; title: string; dueDate: string; assignedStudentCount: number }>(
      `${this.url}/assignments`,
      request,
      { headers: { 'Idempotency-Key': idempotencyKey } }
    );
  }

  updateAssignment(id: string, request: CoachingAdminAssignmentUpdateRequest) {
    return this.http.put<{ assignmentId: string; dueDate: string; assignedStudentCount: number }>(
      `${this.url}/assignments/${encodeURIComponent(id)}`,
      request
    );
  }

  cancelAssignment(id: string) {
    return this.http.post(`${this.url}/assignments/${encodeURIComponent(id)}/cancel`, {});
  }

  deleteAssignment(id: string) {
    return this.http.delete(`${this.url}/assignments/${encodeURIComponent(id)}`);
  }

  gradeAssignment(id: string, request: { assignmentId: string; studentId: string; score: number; teacherFeedback?: string }) {
    return this.http.post(`${this.url}/assignments/${encodeURIComponent(id)}/grade`, request);
  }

  createSession(request: CoachingAdminSessionCreateRequest, idempotencyKey: string) {
    return this.http.post<{ sessionId: string }>(`${this.url}/sessions`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  updateSession(id: string, request: CoachingAdminSessionUpdateRequest) {
    return this.http.put<{ sessionId: string; scheduledDate: string }>(
      `${this.url}/sessions/${encodeURIComponent(id)}`,
      request
    );
  }

  updateSessionAttendance(id: string, request: { sessionId: string; attended: boolean; notes?: string; studentId?: string }) {
    return this.http.post(`${this.url}/sessions/${encodeURIComponent(id)}/attendance`, request);
  }

  cancelSession(id: string) {
    return this.http.post(`${this.url}/sessions/${encodeURIComponent(id)}/cancel`, {});
  }

  deleteSession(id: string) {
    return this.http.delete(`${this.url}/sessions/${encodeURIComponent(id)}`);
  }

  createExam(request: CoachingAdminExamCreateRequest, idempotencyKey: string) {
    return this.http.post<{ examId: string }>(`${this.url}/exams`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  addExamResult(id: string, request: {
    examId: string;
    studentId: string;
    score: number;
    correctAnswers: number;
    wrongAnswers: number;
    emptyAnswers: number;
    subjectScores?: Record<string, number>;
    notes?: string;
  }, idempotencyKey: string) {
    return this.http.post(`${this.url}/exams/${encodeURIComponent(id)}/results`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  updateExam(id: string, request: CoachingAdminExamUpdateRequest) {
    return this.http.put<{ examId: string; examDate: string; maxScore: number }>(
      `${this.url}/exams/${encodeURIComponent(id)}`,
      request
    );
  }

  updateExamResult(examId: string, resultId: string, request: CoachingAdminExamResultUpdateRequest) {
    return this.http.put<{ examId: string; resultId: string; score: number }>(
      `${this.url}/exams/${encodeURIComponent(examId)}/results/${encodeURIComponent(resultId)}`,
      request
    );
  }

  deleteExamResult(examId: string, resultId: string) {
    return this.http.delete<void>(
      `${this.url}/exams/${encodeURIComponent(examId)}/results/${encodeURIComponent(resultId)}`
    );
  }

  deleteExam(id: string) {
    return this.http.delete(`${this.url}/exams/${encodeURIComponent(id)}`);
  }

  createGoal(request: CoachingAdminGoalCreateRequest, idempotencyKey: string) {
    return this.http.post<{ goalId: string }>(`${this.url}/goals`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  getGoal(id: string) {
    return this.http.get<CoachingAdminGoalDetail>(`${this.url}/goals/${encodeURIComponent(id)}`);
  }

  updateGoal(id: string, request: CoachingAdminGoalUpdateRequest) {
    return this.http.put<{ goalId: string; title: string }>(
      `${this.url}/goals/${encodeURIComponent(id)}`,
      request
    );
  }

  updateGoalProgress(id: string, progress: number) {
    return this.http.put(`${this.url}/goals/${encodeURIComponent(id)}/progress`, {
      goalId: id,
      progress
    });
  }

  deleteGoal(id: string) {
    return this.http.delete(`${this.url}/goals/${encodeURIComponent(id)}`);
  }

  getSessions(options: { pageNumber?: number; pageSize?: number; status?: string; search?: string } = {}) {
    return this.http.get<CoachingAdminSessionPage>(`${this.url}/sessions`, {
      params: this.listParams(options)
    });
  }

  getSession(id: string) {
    return this.http.get<CoachingAdminSessionDetail>(`${this.url}/sessions/${encodeURIComponent(id)}`);
  }

  getExams(options: { pageNumber?: number; pageSize?: number; examType?: string; search?: string } = {}) {
    return this.http.get<CoachingAdminExamPage>(`${this.url}/exams`, {
      params: this.listParams(options, 'examType')
    });
  }

  getExam(id: string) {
    return this.http.get<CoachingAdminExamDetail>(`${this.url}/exams/${encodeURIComponent(id)}`);
  }

  getGoals(options: { pageNumber?: number; pageSize?: number; completed?: boolean; search?: string } = {}) {
    let params = this.listParams(options);
    if (options.completed !== undefined) params = params.set('completed', options.completed);
    return this.http.get<CoachingAdminGoalPage>(`${this.url}/goals`, { params });
  }

  attachmentUrl(assignmentId: string, studentId: string, attachmentId: string) {
    return `${environment.apiUrl}/assignments/${encodeURIComponent(assignmentId)}/students/${encodeURIComponent(studentId)}/attachments/${encodeURIComponent(attachmentId)}/content`;
  }

  downloadAttachment(assignmentId: string, studentId: string, attachmentId: string) {
    return this.http.get(this.attachmentUrl(assignmentId, studentId, attachmentId), {
      responseType: 'blob'
    });
  }

  private listParams(options: Record<string, unknown>, filterKey?: string) {
    let params = new HttpParams()
      .set('pageNumber', String(options['pageNumber'] ?? 1))
      .set('pageSize', String(options['pageSize'] ?? 25));
    const key = filterKey ?? 'status';
    if (options[key]) params = params.set(key, String(options[key]));
    if (options['search'] && String(options['search']).trim()) params = params.set('search', String(options['search']).trim());
    return params;
  }
}
