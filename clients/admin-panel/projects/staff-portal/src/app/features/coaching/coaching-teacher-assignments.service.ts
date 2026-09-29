import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherAssignment {
  id: string;
  title: string;
  type: string;
  dueDate: string;
  status: string;
  totalStudents: number;
  submittedCount: number;
  createdAt: string;
}

export interface PagedCoachingAssignments<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export interface CoachingAssignmentDetail extends CoachingTeacherAssignment {
  teacherId: string;
  institutionId?: string;
  description?: string;
  subject?: string;
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
  estimatedDurationMinutes?: number;
  maxScore?: number;
  passingScore?: number;
  assignedStudents: Array<{
    studentId: string;
    status: string;
    submittedAt?: string;
    score?: number;
    teacherFeedback?: string;
    attachments?: CoachingAssignmentAttachment[];
  }>;
}

export interface CoachingAssignmentAttachment {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  uploadedAt?: string;
  scannedAt?: string;
}

interface AssignmentFields {
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

export interface CoachingAssignmentCreateRequest extends AssignmentFields {
  teacherId: string;
  assignmentType: string;
  assignmentSource: string;
  studentIds: string[];
}

export interface CoachingAssignmentUpdateRequest extends AssignmentFields {
  assignmentId: string;
  studentIds: string[];
}

export interface CoachingAssignmentMutationResponse {
  assignmentId: string;
  title?: string;
  dueDate: string;
  assignedStudentCount: number;
}

export interface CoachingGradeResponse {
  assignmentId: string;
  studentId: string;
  score: number;
  status: string;
  gradedAt: string;
}

export type CoachingAssignmentStatus = 'Active' | 'Completed' | 'Cancelled';

@Injectable({ providedIn: 'root' })
export class CoachingTeacherAssignmentsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/assignments`;

  getTeacherAssignments(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25,
    status?: CoachingAssignmentStatus
  ): Observable<PagedCoachingAssignments<CoachingTeacherAssignment>> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    if (status) params = params.set('status', status);

    return this.http.get<PagedCoachingAssignments<CoachingTeacherAssignment>>(
      `${this.url}/teacher/${encodeURIComponent(teacherId)}`,
      { params }
    );
  }

  getAssignment(assignmentId: string): Observable<CoachingAssignmentDetail> {
    return this.http.get<CoachingAssignmentDetail>(`${this.url}/${encodeURIComponent(assignmentId)}`);
  }

  createAssignment(
    request: CoachingAssignmentCreateRequest,
    idempotencyKey: string
  ): Observable<CoachingAssignmentMutationResponse> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<CoachingAssignmentMutationResponse>(
      this.url,
      this.normalizeRequest(request),
      { headers }
    );
  }

  updateAssignment(
    assignmentId: string,
    request: CoachingAssignmentUpdateRequest
  ): Observable<CoachingAssignmentMutationResponse> {
    return this.http.put<CoachingAssignmentMutationResponse>(
      `${this.url}/${encodeURIComponent(assignmentId)}`,
      { ...this.normalizeRequest(request), assignmentId }
    );
  }

  gradeAssignment(
    assignmentId: string,
    studentId: string,
    score: number,
    teacherFeedback?: string
  ): Observable<CoachingGradeResponse> {
    return this.http.post<CoachingGradeResponse>(
      `${this.url}/${encodeURIComponent(assignmentId)}/grade`,
      {
        assignmentId,
        studentId,
        score,
        teacherFeedback: teacherFeedback?.trim() || null
      }
    );
  }

  downloadAttachment(assignmentId: string, studentId: string, attachmentId: string): Observable<Blob> {
    return this.http.get(
      `${this.url}/${encodeURIComponent(assignmentId)}/students/${encodeURIComponent(studentId)}/attachments/${encodeURIComponent(attachmentId)}/content`,
      { responseType: 'blob' }
    );
  }

  cancelAssignment(assignmentId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.url}/${encodeURIComponent(assignmentId)}/cancel`,
      {}
    );
  }

  private normalizeRequest<T extends AssignmentFields>(request: T): Record<string, unknown> {
    const body = {
      ...request,
      title: request.title.trim(),
      description: request.description?.trim() || null,
      subject: request.subject?.trim() || null,
      studentIds: request.studentIds
        ? [...new Set(request.studentIds.map(studentId => studentId.trim()).filter(Boolean))]
        : null
    } as unknown as Record<string, unknown>;

    for (const field of ['bookTitle', 'bookIsbn', 'bookEdition', 'bookChapter']) {
      const value = request[field as keyof AssignmentFields];
      if (value === undefined) delete body[field];
      else body[field] = typeof value === 'string' ? value.trim() || null : value;
    }

    return body;
  }
}
