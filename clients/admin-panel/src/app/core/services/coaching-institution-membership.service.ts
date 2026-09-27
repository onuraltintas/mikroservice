import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherInvitation {
  teacherEmail: string;
  message?: string;
}

export interface CoachingStudentInvitation {
  studentEmail: string;
  message?: string;
  teacherUserId?: string;
}

export interface CoachingInstitutionStudentUpdate {
  gradeLevel: number;
  teacherUserId: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingInstitutionMembershipService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/institution`;

  inviteTeacher(request: CoachingTeacherInvitation) {
    return this.http.post<{ invitationId: string }>(`${this.url}/invite-teacher`, request);
  }

  inviteStudent(request: CoachingStudentInvitation) {
    return this.http.post<{ invitationId: string }>(`${this.url}/invite-student`, request);
  }

  updateStudent(studentUserId: string, request: CoachingInstitutionStudentUpdate) {
    return this.http.put<void>(`${this.url}/students/${encodeURIComponent(studentUserId)}`, request);
  }

  removeTeacher(teacherUserId: string) {
    return this.http.delete<void>(`${this.url}/teachers/${encodeURIComponent(teacherUserId)}`);
  }

  removeStudent(studentUserId: string) {
    return this.http.delete<void>(`${this.url}/students/${encodeURIComponent(studentUserId)}`);
  }
}
