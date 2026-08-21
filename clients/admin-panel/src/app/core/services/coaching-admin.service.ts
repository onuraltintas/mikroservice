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
  studentId: string;
  score: number;
  correctAnswers?: number;
  wrongAnswers?: number;
  emptyAnswers?: number;
  subjectScores?: Record<string, number>;
  teacherNotes?: string;
}

export interface CoachingAdminExamDetail {
  id: string;
  createdByTeacherId: string;
  institutionId?: string;
  title: string;
  examType: string;
  examDate: string;
  maxScore: number;
  description?: string;
  results: CoachingAdminExamResult[];
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

@Injectable({ providedIn: 'root' })
export class CoachingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching-admin`;

  getOverview(recentLimit = 10) {
    const params = new HttpParams().set('recentLimit', recentLimit);
    return this.http.get<CoachingAdminOverview>(`${this.url}/overview`, { params });
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

  deleteExam(id: string) {
    return this.http.delete(`${this.url}/exams/${encodeURIComponent(id)}`);
  }

  createGoal(request: CoachingAdminGoalCreateRequest, idempotencyKey: string) {
    return this.http.post<{ goalId: string }>(`${this.url}/goals`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
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
