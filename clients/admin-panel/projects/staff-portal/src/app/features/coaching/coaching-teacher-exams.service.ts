import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherExam {
  id: string;
  title: string;
  examType: string;
  examDate: string;
  maxScore: number;
  subject?: string | null;
  resultCount: number;
  description?: string | null;
  durationMinutes?: number | null;
  targetGradeLevel?: number | null;
}

export interface CoachingTeacherExamResult {
  id: string;
  studentId: string;
  score: number;
  correctAnswers?: number | null;
  wrongAnswers?: number | null;
  emptyAnswers?: number | null;
  subjectScores?: Record<string, number> | null;
  ranking?: number | null;
  teacherNotes?: string | null;
}

export interface CoachingTeacherExamDetail extends CoachingTeacherExam {
  results: CoachingTeacherExamResult[];
  resultPageNumber: number;
  resultPageSize: number;
  resultTotalPages: number;
}

export interface CoachingTeacherExamPage {
  items: CoachingTeacherExam[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export interface CoachingTeacherExamCreateRequest {
  teacherId: string;
  title: string;
  type: number;
  examDate: string;
  maxScore: number;
  institutionId?: string | null;
  description?: string | null;
}

export interface CoachingTeacherExamUpdateRequest {
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

export interface CoachingTeacherExamResultRequest {
  examId: string;
  resultId?: string;
  studentId?: string;
  score: number;
  correctAnswers: number;
  wrongAnswers: number;
  emptyAnswers: number;
  subjectScores?: Record<string, number> | null;
  ranking?: number | null;
  notes?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherExamsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/exams`;

  getTeacherExams(teacherId: string, pageNumber = 1, pageSize = 25): Observable<CoachingTeacherExamPage> {
    return this.http.get<CoachingTeacherExamPage>(
      `${this.url}/teacher/${encodeURIComponent(teacherId)}`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  createExam(request: CoachingTeacherExamCreateRequest, idempotencyKey: string): Observable<{ examId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ examId: string }>(this.url, {
      ...request,
      title: request.title.trim(),
      institutionId: request.institutionId || null,
      description: request.description?.trim() || null
    }, { headers });
  }

  updateExam(examId: string, request: CoachingTeacherExamUpdateRequest): Observable<{ examId: string; examDate: string; maxScore: number }> {
    return this.http.put<{ examId: string; examDate: string; maxScore: number }>(
      `${this.url}/${encodeURIComponent(examId)}`,
      {
        ...request,
        examId,
        title: request.title.trim(),
        subject: request.subject?.trim() || null,
        description: request.description?.trim() || null
      }
    );
  }

  getExamDetail(examId: string, pageNumber = 1, pageSize = 25): Observable<CoachingTeacherExamDetail> {
    return this.http.get<CoachingTeacherExamDetail>(
      `${this.url}/${encodeURIComponent(examId)}/teacher-detail`,
      { params: this.paging(pageNumber, pageSize) }
    );
  }

  addExamResult(
    examId: string,
    request: CoachingTeacherExamResultRequest,
    idempotencyKey: string
  ): Observable<{ message: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ message: string }>(
      `${this.url}/${encodeURIComponent(examId)}/results`,
      { ...request, examId, notes: request.notes?.trim() || null },
      { headers }
    );
  }

  updateExamResult(
    examId: string,
    resultId: string,
    request: CoachingTeacherExamResultRequest
  ): Observable<{ examId: string; resultId: string; score: number }> {
    return this.http.put<{ examId: string; resultId: string; score: number }>(
      `${this.url}/${encodeURIComponent(examId)}/results/${encodeURIComponent(resultId)}`,
      { ...request, examId, resultId, notes: request.notes?.trim() || null }
    );
  }

  private paging(pageNumber: number, pageSize: number): HttpParams {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    return new HttpParams().set('pageNumber', page).set('pageSize', size);
  }
}
