import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { CoachingAdminAssignmentCreateRequest, CoachingAdminAssignmentUpdateRequest, CoachingAdminSessionCreateRequest, CoachingAdminSessionUpdateRequest } from './coaching-admin.service';

export interface InstitutionStudent {
  userId: string;
  firstName: string;
  lastName: string;
  email?: string;
  gradeLevel?: number | null;
  isActive?: boolean;
  teacherUserId?: string | null;
  teacherName?: string | null;
}

export interface InstitutionStudentPage {
  items: InstitutionStudent[];
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class InstitutionCoachingService {
  private readonly http = inject(HttpClient);
  private readonly identityUrl = `${environment.apiUrl}/institution`;
  private readonly coachingUrl = `${environment.apiUrl}/institution-coaching`;

  getStudents(page = 1, search = '') {
    let params = new HttpParams().set('page', page).set('pageSize', 100);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<InstitutionStudentPage>(`${this.identityUrl}/students`, { params });
  }

  assignTeacher(student: InstitutionStudent, teacherUserId: string | null) {
    return this.http.put<void>(`${this.identityUrl}/students/${encodeURIComponent(student.userId)}`, {
      firstName: student.firstName,
      lastName: student.lastName,
      gradeLevel: student.gradeLevel ?? null,
      teacherUserId
    });
  }

  createAssignment(request: CoachingAdminAssignmentCreateRequest, idempotencyKey: string) {
    return this.http.post<{ assignmentId: string }>(`${this.coachingUrl}/assignments`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  createSession(request: CoachingAdminSessionCreateRequest, idempotencyKey: string) {
    return this.http.post<{ sessionId: string }>(`${this.coachingUrl}/sessions`, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }

  cancelAssignment(id: string) {
    return this.http.post<void>(`${this.coachingUrl}/assignments/${encodeURIComponent(id)}/cancel`, {});
  }

  updateAssignment(id: string, request: CoachingAdminAssignmentUpdateRequest) {
    return this.http.put(`${this.coachingUrl}/assignments/${encodeURIComponent(id)}`, request);
  }

  updateSession(id: string, request: CoachingAdminSessionUpdateRequest) {
    return this.http.put(`${this.coachingUrl}/sessions/${encodeURIComponent(id)}`, request);
  }

  gradeAssignment(id: string, studentId: string, score: number, teacherFeedback?: string) {
    return this.http.post(`${this.coachingUrl}/assignments/${encodeURIComponent(id)}/grade`, {
      assignmentId: id, studentId, score, teacherFeedback
    });
  }

  updateAttendance(id: string, studentId: string, attended: boolean, notes?: string) {
    return this.http.post(`${this.coachingUrl}/sessions/${encodeURIComponent(id)}/attendance`, {
      sessionId: id, studentId, attended, notes
    });
  }

  cancelSession(id: string) {
    return this.http.post<void>(`${this.coachingUrl}/sessions/${encodeURIComponent(id)}/cancel`, {});
  }
}
