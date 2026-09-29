import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherSessionReflection {
  studentId: string;
  note: string | null;
  attendanceStatus: string;
}

export interface CoachingTeacherSession {
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
  studentReflections?: CoachingTeacherSessionReflection[];
  teacherNotes?: string;
  teacherNotesVisibility?: string;
}

export interface PagedCoachingTeacherSessions {
  items: CoachingTeacherSession[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export type CoachingNoteVisibility = 'CoachPrivate' | 'StudentVisible' | 'GuardianVisible' | 'InstitutionVisible';

export interface CoachingTeacherSessionCreateRequest {
  teacherId: string;
  studentId: string;
  startTime: string;
  durationMinutes: number;
  subject: string | null;
  notes: string | null;
  type: 'OneOnOne' | 'Group';
  studentIds: string[];
  meetingLink: string | null;
  teacherNotesVisibility: CoachingNoteVisibility;
}

export interface CoachingTeacherSessionUpdateRequest {
  sessionId: string;
  title: string;
  description: string | null;
  scheduledDate: string;
  durationMinutes: number;
  meetingLink: string | null;
  teacherNotes: string | null;
  teacherNotesVisibility: CoachingNoteVisibility;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherSessionsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/sessions`;

  getTeacherSessions(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PagedCoachingTeacherSessions> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    const params = new HttpParams().set('pageNumber', page).set('pageSize', size);

    return this.http.get<PagedCoachingTeacherSessions>(
      `${this.url}/teacher/${encodeURIComponent(teacherId)}`,
      { params }
    );
  }

  getSession(sessionId: string): Observable<CoachingTeacherSession> {
    return this.http.get<CoachingTeacherSession>(`${this.url}/${encodeURIComponent(sessionId)}`);
  }

  createSession(
    request: CoachingTeacherSessionCreateRequest,
    idempotencyKey: string
  ): Observable<{ sessionId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ sessionId: string }>(this.url, {
      ...request,
      subject: request.subject?.trim() || null,
      notes: request.notes?.trim() || null,
      meetingLink: request.meetingLink?.trim() || null,
      studentIds: [...new Set(request.studentIds.map(studentId => studentId.trim()).filter(Boolean))]
    }, { headers });
  }

  updateSession(
    sessionId: string,
    request: CoachingTeacherSessionUpdateRequest
  ): Observable<{ sessionId: string; scheduledDate: string }> {
    return this.http.put<{ sessionId: string; scheduledDate: string }>(
      `${this.url}/${encodeURIComponent(sessionId)}`,
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

  cancelSession(sessionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.url}/${encodeURIComponent(sessionId)}/cancel`,
      {}
    );
  }

  updateAttendance(
    sessionId: string,
    studentId: string,
    attended: boolean
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.url}/${encodeURIComponent(sessionId)}/attendance`,
      { sessionId, studentId, attended, notes: null }
    );
  }

  downloadCalendarFeed(): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/calendar/teacher.ics`, { responseType: 'blob' });
  }
}
