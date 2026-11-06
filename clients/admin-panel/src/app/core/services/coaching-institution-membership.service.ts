import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import type { EducationStatus } from '../../features/coaching-portal/coaching-education';
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
  /** E3 #14: the education status; a grade (5–12) only for a student. Without it the server reads a student's grade. */
  educationStatus?: EducationStatus;
  gradeLevel: number | null;
  teacherUserId: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingInstitutionMembershipService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/institution`;

  private endpoint(institutionId?: string) {
    return institutionId ? `${environment.apiUrl}/coaching/institutions/${encodeURIComponent(institutionId)}` : this.url;
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
