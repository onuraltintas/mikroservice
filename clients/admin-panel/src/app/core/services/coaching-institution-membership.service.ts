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

  private endpoint(institutionId?: string) {
    return institutionId ? `${environment.apiUrl}/institutions/${encodeURIComponent(institutionId)}/coaching` : this.url;
  }

  inviteTeacher(request: CoachingTeacherInvitation, institutionId?: string) {
    return this.http.post<{ invitationId: string }>(`${this.endpoint(institutionId)}/invite-teacher`, request);
  }

  inviteStudent(request: CoachingStudentInvitation, institutionId?: string) {
    return this.http.post<{ invitationId: string }>(`${this.endpoint(institutionId)}/invite-student`, request);
  }

  updateStudent(studentUserId: string, request: CoachingInstitutionStudentUpdate, institutionId?: string) {
    return this.http.put<void>(`${this.endpoint(institutionId)}/students/${encodeURIComponent(studentUserId)}`, request);
  }

  removeTeacher(teacherUserId: string, institutionId?: string) {
    return this.http.delete<void>(`${this.endpoint(institutionId)}/teachers/${encodeURIComponent(teacherUserId)}`);
  }

  removeStudent(studentUserId: string, institutionId?: string) {
    return this.http.delete<void>(`${this.endpoint(institutionId)}/students/${encodeURIComponent(studentUserId)}`);
  }
}
