import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Roadmap O12: one of the student's teachers; only independent links can be ended by the student. */
export interface CoachingStudentTeacher { teacherUserId: string; teacherName: string; institutionId: string | null; since: string; canLeave: boolean }

@Injectable({ providedIn: 'root' })
export class CoachingStudentTeachersService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/students/me/teachers`;
  list() { return this.http.get<CoachingStudentTeacher[]>(this.url); }
  leave(teacherUserId: string) { return this.http.delete<void>(`${this.url}/${encodeURIComponent(teacherUserId)}`); }
}
