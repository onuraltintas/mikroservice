import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export interface StudentAssignment {
  id: string;
  title: string;
  subject?: string;
  dueDate: string;
  status: string;
  submittedAt?: string;
  score?: number;
  maxScore?: number;
  teacherFeedback?: string;
  isOverdue: boolean;
}

export interface TeacherAssignment {
  id: string;
  title: string;
  type: string;
  dueDate: string;
  status: string;
  totalStudents: number;
  submittedCount: number;
  createdAt: string;
}

export interface AssignmentAttachment {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  uploadedAt?: string;
  scannedAt?: string;
}

export interface AssignedStudent {
  studentId: string;
  submittedAt?: string;
  score?: number;
  teacherFeedback?: string;
  status: string;
  attachments?: AssignmentAttachment[];
}

export interface AssignmentDetail {
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
  assignedStudents: AssignedStudent[];
  createdAt: string;
}

export interface SubmitAssignmentResponse {
  assignmentId: string;
  studentId: string;
  submittedAt: string;
  status: string;
}

export interface GradeAssignmentResponse {
  assignmentId: string;
  studentId: string;
  score: number;
  status: string;
  gradedAt: string;
}

export interface CreateAttachmentResponse {
  assignmentId: string;
  studentId: string;
  attachmentId: string;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
  status: string;
}

export interface UploadAttachmentResponse {
  attachmentId: string;
  sizeBytes: number;
  sha256: string;
  status: string;
  uploadedAt: string;
}

export interface Goal {
  id: string;
  title: string;
  description?: string;
  category: string;
  targetDate?: string;
  targetScore?: number;
  progress: number;
  isCompleted: boolean;
  completedAt?: string;
}

export interface CreateStudentGoalRequest {
  title: string;
  category: number;
  description?: string | null;
  targetDate?: string | null;
  targetScore?: number | null;
}

export interface CreateGoalResponse {
  goalId: string;
}

export interface UpdateGoalProgressResponse {
  message: string;
}

export interface StudentProgressSummary {
  studentId: string;
  totalAssignments: number;
  submittedAssignments: number;
  gradedAssignments: number;
  averageAssignmentPercentage?: number;
  totalExams: number;
  averageExamPercentage?: number;
  totalGoals: number;
  completedGoals: number;
  averageGoalProgress: number;
  totalSessions: number;
  upcomingSessions: number;
  attendedSessions: number;
  attendancePercentage?: number;
}

export interface ExamResult {
  examId: string;
  examTitle: string;
  examDate: string;
  examType: string;
  score: number;
  maxScore: number;
  correctAnswers?: number;
  wrongAnswers?: number;
  emptyAnswers?: number;
  subjectScores?: Record<string, number>;
}

export interface TeacherExam {
  id: string;
  title: string;
  examType: string;
  examDate: string;
  maxScore: number;
  subject?: string;
  resultCount: number;
  description?: string;
  durationMinutes?: number;
  targetGradeLevel?: number;
}

export interface TeacherGoal {
  id: string;
  studentId: string;
  title: string;
  description?: string;
  category: string;
  targetDate?: string;
  targetScore?: number;
  targetExamType?: string;
  targetSubject?: string;
  progress: number;
  isCompleted: boolean;
  completedAt?: string;
}

export interface TeacherExamCreateRequest {
  teacherId: string;
  title: string;
  type: number;
  examDate: string;
  maxScore: number;
  institutionId?: string | null;
  description?: string | null;
}

export interface TeacherExamUpdateRequest {
  examId: string;
  title: string;
  type: number;
  subject?: string | null;
  description?: string | null;
  examDate: string;
  durationMinutes?: number | null;
  maxScore: number;
  targetGradeLevel?: number | null;
}

export interface TeacherGoalCreateRequest {
  teacherId: string;
  studentId: string;
  title: string;
  category: number;
  description?: string | null;
  targetDate?: string | null;
  targetScore?: number | null;
}

export interface TeacherGoalUpdateRequest {
  goalId: string;
  title: string;
  description?: string | null;
  category: number;
  targetDate?: string | null;
  targetScore?: number | null;
  targetExamType?: number | null;
  targetSubject?: string | null;
}

export interface CoachingSession {
  id: string;
  studentId: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  subject?: string;
  status: string;
  type: string;
  studentIds: string[];
  meetingLink?: string;
  studentNote?: string;
  studentReflections?: CoachingStudentReflection[];
  teacherNotes?: string;
}

export interface CoachingStudentReflection {
  studentId: string;
  note: string;
  attendanceStatus: string;
}

export interface TeacherSessionCreateRequest {
  teacherId: string;
  studentId: string;
  startTime: string;
  durationMinutes: number;
  subject?: string | null;
  notes?: string | null;
  type: string;
  studentIds?: string[] | null;
  meetingLink?: string | null;
}

export interface TeacherSessionUpdateRequest {
  sessionId: string;
  title: string;
  description?: string | null;
  scheduledDate: string;
  durationMinutes: number;
  meetingLink?: string | null;
  teacherNotes?: string | null;
}

export interface TeacherSessionMutationResponse {
  sessionId: string;
  scheduledDate?: string;
}

export interface ChildSummary {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gradeLevel?: number;
  institutionId?: string;
  institutionName?: string;
  avatarUrl?: string;
}

export interface TeacherStudent {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gradeLevel?: number;
  institutionId?: string;
  institutionName?: string;
  avatarUrl?: string;
  subject?: string;
  assignmentStartDate: string;
}

export interface TeacherAssignmentCreateRequest {
  teacherId: string;
  institutionId?: string | null;
  title: string;
  description?: string | null;
  subject?: string | null;
  assignmentType: string;
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
  studentIds: string[];
}

export interface TeacherAssignmentUpdateRequest {
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

export interface TeacherAssignmentMutationResponse {
  assignmentId: string;
  title?: string;
  dueDate: string;
  assignedStudentCount: number;
}

@Injectable({ providedIn: 'root' })
export class CoachingPortalService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/assignments`;

  getStudentAssignments(
    studentId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<StudentAssignment>> {
    return this.http.get<PagedResponse<StudentAssignment>>(
      `${this.url}/student/${this.id(studentId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  getTeacherAssignments(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<TeacherAssignment>> {
    return this.http.get<PagedResponse<TeacherAssignment>>(
      `${this.url}/teacher/${this.id(teacherId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  createTeacherAssignment(
    request: TeacherAssignmentCreateRequest,
    idempotencyKey: string
  ): Observable<TeacherAssignmentMutationResponse> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    const body = this.normalizeAssignmentRequest(request);
    return this.http.post<TeacherAssignmentMutationResponse>(
      `${this.url}`,
      body,
      { headers }
    );
  }

  updateTeacherAssignment(
    assignmentId: string,
    request: TeacherAssignmentUpdateRequest
  ): Observable<TeacherAssignmentMutationResponse> {
    const body = { ...this.normalizeAssignmentRequest(request), assignmentId };
    return this.http.put<TeacherAssignmentMutationResponse>(
      `${this.url}/${this.id(assignmentId)}`,
      body
    );
  }

  cancelTeacherAssignment(assignmentId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.url}/${this.id(assignmentId)}/cancel`,
      {}
    );
  }

  getTeacherStudents(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string
  ): Observable<PagedResponse<TeacherStudent>> {
    let params = this.paging(pageNumber, pageSize);
    const search = searchTerm?.trim();
    if (search) {
      params = params.set('searchTerm', search);
    }

    return this.http.get<PagedResponse<TeacherStudent>>(
      `${environment.apiUrl}/teachers/me/students`,
      { params }
    );
  }

  getAssignment(assignmentId: string): Observable<AssignmentDetail> {
    return this.http.get<AssignmentDetail>(`${this.url}/${this.id(assignmentId)}`);
  }

  submitAssignment(
    assignmentId: string,
    studentId: string,
    studentNote?: string
  ): Observable<SubmitAssignmentResponse> {
    const note = studentNote?.trim();
    return this.http.post<SubmitAssignmentResponse>(
      `${this.url}/${this.id(assignmentId)}/submit`,
      {
        assignmentId,
        studentId,
        studentNote: note || null
      }
    );
  }

  gradeAssignment(
    assignmentId: string,
    studentId: string,
    score: number,
    teacherFeedback?: string
  ): Observable<GradeAssignmentResponse> {
    const feedback = teacherFeedback?.trim();
    return this.http.post<GradeAssignmentResponse>(
      `${this.url}/${this.id(assignmentId)}/grade`,
      {
        assignmentId,
        studentId,
        score,
        teacherFeedback: feedback || null
      }
    );
  }

  createAttachment(
    assignmentId: string,
    studentId: string,
    file: File,
    sha256: string
  ): Observable<CreateAttachmentResponse> {
    return this.http.post<CreateAttachmentResponse>(
      `${this.url}/${this.id(assignmentId)}/students/${this.id(studentId)}/attachments`,
      {
        assignmentId,
        studentId,
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        sha256
      }
    );
  }

  uploadAttachment(
    assignmentId: string,
    studentId: string,
    attachmentId: string,
    file: File,
    sha256: string
  ): Observable<UploadAttachmentResponse> {
    const headers = new HttpHeaders({
      'Content-Type': file.type,
      'X-Content-SHA256': sha256
    });

    return this.http.put<UploadAttachmentResponse>(
      `${this.url}/${this.id(assignmentId)}/students/${this.id(studentId)}/attachments/${this.id(attachmentId)}/content`,
      file,
      { headers }
    );
  }

  downloadAttachment(
    assignmentId: string,
    studentId: string,
    attachmentId: string
  ): Observable<Blob> {
    return this.http.get(
      `${this.url}/${this.id(assignmentId)}/students/${this.id(studentId)}/attachments/${this.id(attachmentId)}/content`,
      { responseType: 'blob' }
    );
  }

  getStudentGoals(
    studentId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<Goal>> {
    return this.http.get<PagedResponse<Goal>>(
      `${environment.apiUrl}/goals/student/${this.id(studentId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  getStudentProgress(studentId: string): Observable<StudentProgressSummary> {
    return this.http.get<StudentProgressSummary>(
      `${environment.apiUrl}/reports/student/${this.id(studentId)}/progress`
    );
  }

  createStudentGoal(
    studentId: string,
    request: CreateStudentGoalRequest,
    idempotencyKey: string
  ): Observable<CreateGoalResponse> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<CreateGoalResponse>(
      `${environment.apiUrl}/goals`,
      {
        studentId,
        title: request.title.trim(),
        category: request.category,
        teacherId: null,
        description: request.description?.trim() || null,
        targetDate: request.targetDate || null,
        targetScore: request.targetScore ?? null
      },
      { headers }
    );
  }

  updateGoalProgress(goalId: string, progress: number): Observable<UpdateGoalProgressResponse> {
    return this.http.put<UpdateGoalProgressResponse>(
      `${environment.apiUrl}/goals/${this.id(goalId)}/progress`,
      { goalId, progress }
    );
  }

  updateStudentSessionNote(
    sessionId: string,
    studentId: string,
    note: string
  ): Observable<void> {
    return this.http.put<void>(
      `${environment.apiUrl}/sessions/${this.id(sessionId)}/student-note`,
      { sessionId, studentId, note: note.trim() || null }
    );
  }

  getStudentExamResults(
    studentId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<ExamResult>> {
    return this.http.get<PagedResponse<ExamResult>>(
      `${environment.apiUrl}/exams/student/${this.id(studentId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  getTeacherExams(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<TeacherExam>> {
    return this.http.get<PagedResponse<TeacherExam>>(
      `${environment.apiUrl}/exams/teacher/${this.id(teacherId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  createTeacherExam(
    request: TeacherExamCreateRequest,
    idempotencyKey: string
  ): Observable<{ examId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ examId: string }>(
      `${environment.apiUrl}/exams`,
      {
        ...request,
        title: request.title.trim(),
        institutionId: request.institutionId || null,
        description: request.description?.trim() || null
      },
      { headers }
    );
  }

  updateTeacherExam(
    examId: string,
    request: TeacherExamUpdateRequest
  ): Observable<{ examId: string; examDate: string; maxScore: number }> {
    return this.http.put<{ examId: string; examDate: string; maxScore: number }>(
      `${environment.apiUrl}/exams/${this.id(examId)}`,
      {
        ...request,
        examId,
        title: request.title.trim(),
        subject: request.subject?.trim() || null,
        description: request.description?.trim() || null
      }
    );
  }

  getTeacherGoals(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<TeacherGoal>> {
    return this.http.get<PagedResponse<TeacherGoal>>(
      `${environment.apiUrl}/goals/teacher/${this.id(teacherId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  createTeacherGoal(
    request: TeacherGoalCreateRequest,
    idempotencyKey: string
  ): Observable<{ goalId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ goalId: string }>(
      `${environment.apiUrl}/goals`,
      {
        ...request,
        title: request.title.trim(),
        description: request.description?.trim() || null,
        targetDate: request.targetDate || null,
        targetScore: request.targetScore ?? null
      },
      { headers }
    );
  }

  updateTeacherGoal(
    goalId: string,
    request: TeacherGoalUpdateRequest
  ): Observable<{ goalId: string; title: string }> {
    return this.http.put<{ goalId: string; title: string }>(
      `${environment.apiUrl}/goals/${this.id(goalId)}`,
      {
        ...request,
        goalId,
        title: request.title.trim(),
        description: request.description?.trim() || null,
        targetDate: request.targetDate || null,
        targetScore: request.targetScore ?? null,
        targetSubject: request.targetSubject?.trim() || null
      }
    );
  }

  getTeacherSessions(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<CoachingSession>> {
    return this.http.get<PagedResponse<CoachingSession>>(
      `${environment.apiUrl}/sessions/teacher/${this.id(teacherId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  getTeacherSession(sessionId: string): Observable<CoachingSession> {
    return this.http.get<CoachingSession>(
      `${environment.apiUrl}/sessions/${this.id(sessionId)}`
    );
  }

  createTeacherSession(
    request: TeacherSessionCreateRequest,
    idempotencyKey: string
  ): Observable<{ sessionId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ sessionId: string }>(
      `${environment.apiUrl}/sessions`,
      {
        ...request,
        subject: request.subject?.trim() || null,
        notes: request.notes?.trim() || null,
        meetingLink: request.meetingLink?.trim() || null,
        studentIds: request.studentIds?.length ? [...new Set(request.studentIds)] : null
      },
      { headers }
    );
  }

  updateTeacherSession(
    sessionId: string,
    request: TeacherSessionUpdateRequest
  ): Observable<TeacherSessionMutationResponse> {
    return this.http.put<TeacherSessionMutationResponse>(
      `${environment.apiUrl}/sessions/${this.id(sessionId)}`,
      {
        ...request,
        sessionId,
        title: request.title.trim(),
        description: request.description?.trim() || null,
        meetingLink: request.meetingLink?.trim() || null,
        teacherNotes: request.teacherNotes?.trim() || null
      }
    );
  }

  updateSessionAttendance(
    sessionId: string,
    studentId: string,
    attended: boolean,
    notes?: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${environment.apiUrl}/sessions/${this.id(sessionId)}/attendance`,
      {
        sessionId,
        studentId,
        attended,
        notes: notes?.trim() || null
      }
    );
  }

  cancelTeacherSession(sessionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${environment.apiUrl}/sessions/${this.id(sessionId)}/cancel`,
      {}
    );
  }

  getStudentSessions(
    studentId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedResponse<CoachingSession>> {
    return this.http.get<PagedResponse<CoachingSession>>(
      `${environment.apiUrl}/sessions/student/${this.id(studentId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  calendarFeedUrl(audience: 'teacher' | 'student'): string {
    return `${environment.apiUrl}/calendar/${audience}.ics`;
  }

  downloadCalendarFeed(audience: 'teacher' | 'student'): Observable<Blob> {
    return this.http.get(this.calendarFeedUrl(audience), { responseType: 'blob' });
  }

  getMyChildren(): Observable<ChildSummary[]> {
    return this.http.get<ChildSummary[]>(`${environment.apiUrl}/users/me/children`);
  }

  async calculateSha256(file: File): Promise<string> {
    const digest = await globalThis.crypto.subtle.digest('SHA-256', await file.arrayBuffer());
    return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
  }

  private paging(pageNumber: number, pageSize: number): HttpParams {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    return new HttpParams().set('pageNumber', page).set('pageSize', size);
  }

  private id(value: string): string {
    return encodeURIComponent(value);
  }

  private normalizeAssignmentRequest(
    request: TeacherAssignmentCreateRequest | TeacherAssignmentUpdateRequest
  ) {
    const body: Record<string, unknown> = {
      ...request,
      title: request.title.trim(),
      description: request.description?.trim() || null,
      subject: request.subject?.trim() || null,
      studentIds: request.studentIds
        ? [...new Set(request.studentIds.map(studentId => studentId.trim()).filter(Boolean))]
        : null
    };

    for (const field of ['bookTitle', 'bookIsbn', 'bookEdition', 'bookChapter']) {
      const value = request[field as keyof typeof request];
      if (value === undefined) {
        delete body[field];
      } else {
        body[field] = typeof value === 'string' ? value.trim() || null : value;
      }
    }

    return body;
  }
}
