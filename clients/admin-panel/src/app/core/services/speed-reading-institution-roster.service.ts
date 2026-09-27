import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SpeedReadingInstitutionMember {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
  role: 1 | 2;
  isActive: boolean;
  gradeLevel: number | null;
  teacherUserId: string | null;
  teacherName: string | null;
  studentCount: number;
}

export interface SpeedReadingInstitutionMemberPage {
  items: SpeedReadingInstitutionMember[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingInstitutionRosterService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/speed-reading/institutions`;

  getMembers(institutionId: string, pageNumber: number, pageSize: number, role?: 1 | 2,
             searchTerm?: string, teacherUserId?: string) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (role) params = params.set('role', role);
    if (searchTerm?.trim()) params = params.set('searchTerm', searchTerm.trim());
    if (teacherUserId) params = params.set('teacherUserId', teacherUserId);
    return this.http.get<SpeedReadingInstitutionMemberPage>(
      `${this.baseUrl}/${institutionId}/members`, { params });
  }

  setActive(institutionId: string, member: SpeedReadingInstitutionMember, isActive: boolean) {
    return this.http.put<void>(`${this.baseUrl}/${institutionId}/members/${member.userId}`,
      { role: member.role, isActive });
  }

  assignStudent(institutionId: string, teacherUserId: string, studentUserId: string) {
    return this.http.put<void>(
      `${this.baseUrl}/${institutionId}/teachers/${teacherUserId}/students/${studentUserId}`, {});
  }

  removeStudent(institutionId: string, teacherUserId: string, studentUserId: string) {
    return this.http.delete<void>(
      `${this.baseUrl}/${institutionId}/teachers/${teacherUserId}/students/${studentUserId}`);
  }
}
