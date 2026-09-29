import { HttpClient, HttpParams } from '@angular/common/http';
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
