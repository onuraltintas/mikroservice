import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Teacher } from '../models/teacher.model';
import { Student } from '../models/student.model';
import { PagedResult } from '../models/user.model';
import { AuthService } from './auth.service';
import { UsersService } from './users.service';

@Injectable({
  providedIn: 'root'
})
export class TeachersService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly usersService = inject(UsersService);
  private readonly speedReadingApiUrl = `${environment.apiUrl}/speed-reading`;

  getTeachers(searchTerm?: string, institutionId?: string, isActive?: boolean): Observable<Teacher[]> {
    return this.getTeachersPage(1, 100, searchTerm, institutionId, isActive).pipe(map(page => page.items));
  }

  getTeachersPage(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string,
    institutionId?: string,
    isActive?: boolean,
    teacherUserId?: string
  ): Observable<PagedResult<Teacher>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString())
      .set('role', 'Teacher');
    if (searchTerm?.trim()) params = params.set('searchTerm', searchTerm.trim());
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (teacherUserId) params = params.set('memberUserId', teacherUserId);

    return this.resolveInstitutionId(institutionId).pipe(
      switchMap(scopedInstitutionId => this.http.get<any>(
        `${this.speedReadingApiUrl}/institutions/${scopedInstitutionId}/members`, { params })),
      map(result => this.toTeacherPage(result, pageNumber, pageSize))
    );
  }

  getTeacherById(teacherUserId: string, institutionId?: string): Observable<Teacher | null> {
    return this.getTeachersPage(1, 1, undefined, institutionId, undefined, teacherUserId)
      .pipe(map(page => page.items[0] ?? null));
  }

  deleteTeacher(id: string, institutionId?: string): Observable<void> {
    return this.resolveInstitutionId(institutionId).pipe(
      switchMap(scopedInstitutionId => this.http.put<void>(
        `${this.speedReadingApiUrl}/institutions/${scopedInstitutionId}/members/${id}`,
        { role: 2, isActive: false }))
    );
  }

  inviteTeacher(email: string, institutionId?: string): Observable<any> {
    return this.resolveInstitutionId(institutionId).pipe(
      switchMap(scopedInstitutionId => this.http.post<any>(
        `${this.speedReadingApiUrl}/invitations/institutions/${scopedInstitutionId}`,
        { email, role: 2 },
        { headers: { 'X-Skip-Error-Toast': 'true' } }))
    );
  }

  private resolveInstitutionId(institutionId?: string): Observable<string> {
    const currentInstitutionId = institutionId ?? this.authService.currentUserValue?.institutionId;
    if (currentInstitutionId) return of(currentInstitutionId);

    return this.usersService.getMyProfile().pipe(
      map(profile => profile.institutionId),
      switchMap(resolvedInstitutionId => resolvedInstitutionId
        ? of(resolvedInstitutionId)
        : throwError(() => new Error('Hızlı Okuma kurum kapsamı bulunamadı.')))
    );
  }

  getMyStudents(): Observable<Student[]> {
    return this.getMyStudentsPage(1, 100).pipe(map(page => page.items));
  }

  getMyStudentById(studentUserId: string): Observable<Student | null> {
    return this.getMyStudentsPage(1, 1, undefined, undefined, undefined, studentUserId)
      .pipe(map(page => page.items[0] ?? null));
  }

  getMyStudentsPage(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string,
    gradeLevel?: number,
    isActive?: boolean,
    studentUserId?: string
  ): Observable<PagedResult<Student>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());
    if (searchTerm?.trim()) params = params.set('searchTerm', searchTerm.trim());
    if (gradeLevel !== undefined) params = params.set('gradeLevel', gradeLevel.toString());
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (studentUserId) params = params.set('studentUserId', studentUserId);

    return this.http.get<any>(`${this.speedReadingApiUrl}/teachers/me/students`, { params }).pipe(
      map(result => this.toStudentPage(result, pageNumber, pageSize))
    );
  }

  linkStudent(email: string): Observable<any> {
    return this.http.post<any>(`${this.speedReadingApiUrl}/invitations/teachers/me`, { email }, {
      headers: { 'X-Skip-Error-Toast': 'true' }
    });
  }

  unlinkStudent(studentId: string, institutionId?: string): Observable<void> {
    let params = new HttpParams();
    if (institutionId) params = params.set('institutionId', institutionId);
    return this.http.delete<void>(`${this.speedReadingApiUrl}/teachers/me/students/${studentId}`, { params });
  }

  private toStudent(student: any): Student {
    return {
      id: student.id ?? student.userId,
      firstName: student.firstName ?? '',
      lastName: student.lastName ?? '',
      email: student.email ?? '',
      institutionId: student.institutionId ?? undefined,
      institutionName: student.institutionName ?? undefined,
      gradeLevel: student.gradeLevel ?? null,
      currentLevel: student.currentLevel ?? 0,
      targetWPM: student.targetWpm ?? student.targetWPM ?? null,
      targetComprehension: student.targetComprehension ?? null,
      dailyGoalMinutes: student.dailyGoalMinutes ?? null,
      learningStyle: student.learningStyle ?? 'Belirtilmedi',
      lastLoginAt: student.lastLoginAt ? new Date(student.lastLoginAt) : undefined,
      isActive: student.isActive ?? true,
      createdAt: student.createdAt ? new Date(student.createdAt) : new Date(student.assignmentStartDate ?? 0),
      teacherId: student.teacherId ?? undefined,
      teacherName: student.teacherName ?? undefined
    };
  }

  private toStudentPage(result: any, pageNumber: number, pageSize: number): PagedResult<Student> {
    const rows = Array.isArray(result) ? result : (result?.items ?? []);
    return {
      items: rows.map((student: any) => this.toStudent(student)),
      totalCount: result?.totalCount ?? rows.length,
      pageNumber: result?.pageNumber ?? pageNumber,
      pageSize: result?.pageSize ?? pageSize,
      totalPages: Math.max(1, Math.ceil((result?.totalCount ?? rows.length) / (result?.pageSize ?? pageSize))),
      hasPreviousPage: (result?.pageNumber ?? pageNumber) > 1,
      hasNextPage: (result?.pageNumber ?? pageNumber) < Math.ceil((result?.totalCount ?? rows.length) / (result?.pageSize ?? pageSize))
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

  private toTeacherPage(result: any, pageNumber: number, pageSize: number): PagedResult<Teacher> {
    const rows = Array.isArray(result) ? result : (result?.items ?? []);
    const totalCount = result?.totalCount ?? rows.length;
    const currentPage = result?.pageNumber ?? pageNumber;
    const currentPageSize = result?.pageSize ?? pageSize;
    const totalPages = Math.ceil(totalCount / currentPageSize);
    return {
      items: rows.map((teacher: any) => this.toTeacher(teacher)),
      totalCount,
      pageNumber: currentPage,
      pageSize: currentPageSize,
      totalPages,
      hasPreviousPage: currentPage > 1,
      hasNextPage: currentPage < totalPages
    };
  }
}
