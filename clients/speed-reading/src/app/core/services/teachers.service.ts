import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Teacher } from '../models/teacher.model';
import { Student } from '../models/student.model';

@Injectable({
  providedIn: 'root'
})
export class TeachersService {
  private readonly http = inject(HttpClient);
  private readonly teachersApiUrl = `${environment.apiUrl}/teachers`;
  private readonly usersApiUrl = `${environment.apiUrl}/users`;

  getTeachers(searchTerm?: string, _institutionId?: string, isActive?: boolean): Observable<Teacher[]> {
    let params = new HttpParams().set('pageSize', '100').set('role', 'Teacher');
    if (searchTerm) params = params.set('search', searchTerm);
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());

    return this.http.get<any>(this.usersApiUrl, { params }).pipe(
      map(result => (Array.isArray(result) ? result : (result?.items ?? [])).map((teacher: any) => this.toTeacher(teacher)))
    );
  }

  getTeacherById(id: string): Observable<Teacher> {
    return this.http.get<Teacher>(`${this.usersApiUrl}/${id}`);
  }

  deleteTeacher(id: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/institution/teachers/${id}`);
  }

  getMyStudents(): Observable<Student[]> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<any>(`${this.teachersApiUrl}/me/students`, { params }).pipe(
      map(result => {
        const rows = Array.isArray(result) ? result : (result?.items ?? []);
        return rows.map((student: any) => this.toStudent(student));
      })
    );
  }

  linkStudent(email: string): Observable<any> {
    return this.http.post<any>(`${this.teachersApiUrl}/invite-student`, { studentEmail: email }, {
      headers: { 'X-Skip-Error-Toast': 'true' }
    });
  }

  unlinkStudent(studentId: string): Observable<void> {
    return this.http.delete<void>(`${this.teachersApiUrl}/students/${studentId}`);
  }

  private toStudent(student: any): Student {
    return {
      id: student.id ?? student.userId,
      firstName: student.firstName ?? '',
      lastName: student.lastName ?? '',
      email: student.email ?? '',
      institutionId: student.institutionId ?? undefined,
      institutionName: student.institutionName ?? undefined,
      currentLevel: student.gradeLevel ?? student.currentLevel ?? 0,
      targetWPM: student.targetWPM ?? 0,
      targetComprehension: student.targetComprehension ?? 0,
      dailyGoalMinutes: student.dailyGoalMinutes ?? 30,
      learningStyle: student.learningStyle ?? 'Belirtilmedi',
      lastLoginAt: student.lastLoginAt ? new Date(student.lastLoginAt) : undefined,
      isActive: student.isActive ?? true,
      createdAt: student.createdAt ? new Date(student.createdAt) : new Date(student.assignmentStartDate ?? 0),
      teacherId: student.teacherId ?? undefined,
      teacherName: student.teacherName ?? undefined
    };
  }

  private toTeacher(teacher: any): Teacher {
    return {
      id: teacher.id ?? teacher.userId,
      firstName: teacher.firstName ?? '',
      lastName: teacher.lastName ?? '',
      email: teacher.email ?? '',
      institutionId: teacher.institutionId ?? teacher.teacherDetails?.institutionId ?? undefined,
      institutionName: teacher.institutionName ?? teacher.teacherDetails?.institutionName ?? undefined,
      studentCount: teacher.studentCount ?? 0,
      lastLoginAt: teacher.lastLoginAt ? new Date(teacher.lastLoginAt) : undefined,
      isActive: teacher.isActive ?? true,
      createdAt: teacher.createdAt ? new Date(teacher.createdAt) : new Date()
    };
  }
}
