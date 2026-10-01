import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface SpeedReadingPage<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingAssignment {
  id: string;
  teacherId: string;
  teacherName?: string | null;
  title: string;
  description: string;
  exerciseTitle: string;
  exerciseTypeName?: string | null;
  dueDate: string;
  createdAt: string;
  studentCount: number;
  completedCount: number;
  isActive: boolean;
}

export interface SpeedReadingAssignmentStudent {
  studentId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isCompleted: boolean;
  completionDate?: string | null;
  score?: number | null;
}

export interface SpeedReadingAssignmentDetails {
  id: string;
  teacherId: string;
  title: string;
  description: string;
  exerciseTitle: string;
  dueDate: string;
  isActive: boolean;
  createdAt: string;
  students: SpeedReadingAssignmentStudent[];
}

export interface SpeedReadingAssignmentStudentOption {
  id: string;
  firstName: string;
  lastName: string;
  email?: string | null;
}

export interface SpeedReadingAssignmentExerciseType {
  id: string;
  displayName: string;
}

export interface SpeedReadingAssignmentExercise {
  id: string;
  title: string;
  exerciseTypeId: string;
}

export interface CreateSpeedReadingAssignmentRequest {
  teacherId?: string;
  exerciseId: string;
  readingTextId: null;
  studentIds: string[];
  title: string;
  description: string;
  dueDate: string;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingAssignmentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/speed-reading`;

  getTeacherAssignments(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string,
    isActive?: boolean,
    exerciseTypeId?: string,
  ): Observable<SpeedReadingPage<SpeedReadingAssignment>> {
    return this.http.get<SpeedReadingPage<SpeedReadingAssignment>>(
      `${this.baseUrl}/assignments/teacher-assignments`,
      { params: this.assignmentParams(pageNumber, pageSize, searchTerm, isActive, exerciseTypeId) },
    );
  }

  getInstitutionAssignments(
    institutionId: string,
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string,
    isActive?: boolean,
    exerciseTypeId?: string,
    teacherId?: string,
  ): Observable<SpeedReadingPage<SpeedReadingAssignment>> {
    let params = this.assignmentParams(pageNumber, pageSize, searchTerm, isActive, exerciseTypeId);
    if (teacherId) params = params.set('teacherId', teacherId);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignment>>(
      `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/assignments`,
      { params },
    );
  }

  getAssignmentDetails(id: string, institutionId?: string): Observable<SpeedReadingAssignmentDetails> {
    const url = institutionId
      ? `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/assignments/${encodeURIComponent(id)}/details`
      : `${this.baseUrl}/assignments/${encodeURIComponent(id)}/details`;
    return this.http.get<SpeedReadingAssignmentDetails>(url);
  }

  createTeacherAssignment(request: CreateSpeedReadingAssignmentRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/assignments`, request);
  }

  createInstitutionAssignment(
    institutionId: string,
    request: CreateSpeedReadingAssignmentRequest & { teacherId: string },
  ): Observable<string> {
    return this.http.post<string>(
      `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/assignments`,
      request,
    );
  }

  deleteAssignment(id: string, institutionId?: string): Observable<void> {
    const url = institutionId
      ? `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/assignments/${encodeURIComponent(id)}`
      : `${this.baseUrl}/assignments/${encodeURIComponent(id)}`;
    return this.http.delete<void>(url);
  }

  addStudentToTeacherAssignment(assignmentId: string, studentId: string): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/assignments/${encodeURIComponent(assignmentId)}/students`,
      { assignmentId, studentId },
    );
  }

  removeStudentFromTeacherAssignment(assignmentId: string, studentId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/assignments/${encodeURIComponent(assignmentId)}/students/${encodeURIComponent(studentId)}`,
    );
  }

  getExerciseTypes(): Observable<SpeedReadingAssignmentExerciseType[]> {
    const params = new HttpParams().set('pageNumber', 1).set('pageSize', 100).set('isActive', true);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignmentExerciseType>>(
      `${this.baseUrl}/exercise-types`, { params },
    ).pipe(map(page => page.items));
  }

  getExercises(exerciseTypeId: string): Observable<SpeedReadingAssignmentExercise[]> {
    const params = new HttpParams()
      .set('exerciseTypeId', exerciseTypeId)
      .set('pageNumber', 1)
      .set('pageSize', 100);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignmentExercise>>(
      `${this.baseUrl}/exercises`, { params },
    ).pipe(map(page => page.items));
  }

  getTeacherStudents(pageNumber = 1, pageSize = 100): Observable<SpeedReadingPage<SpeedReadingAssignmentStudentOption>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignmentStudentOption>>(
      `${this.baseUrl}/teachers/me/students`, { params },
    );
  }

  getInstitutionTeachers(institutionId: string): Observable<SpeedReadingPage<SpeedReadingAssignmentStudentOption>> {
    const params = new HttpParams().set('role', 'Teacher').set('pageNumber', 1).set('pageSize', 100);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignmentStudentOption>>(
      `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/members`, { params },
    ).pipe(map(page => ({
      ...page,
      items: page.items.map(item => ({ ...item, id: (item as SpeedReadingAssignmentStudentOption & { userId?: string }).userId ?? item.id })),
    })));
  }

  getInstitutionTeacherStudents(
    institutionId: string,
    teacherId: string,
    pageNumber = 1,
    pageSize = 100,
  ): Observable<SpeedReadingPage<SpeedReadingAssignmentStudentOption>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingAssignmentStudentOption & { studentUserId?: string }>>(
      `${this.baseUrl}/institutions/${encodeURIComponent(institutionId)}/teachers/${encodeURIComponent(teacherId)}/students`,
      { params },
    ).pipe(map(page => ({
      ...page,
      items: page.items.map(item => ({ ...item, id: item.studentUserId ?? item.id })),
    })));
  }

  private assignmentParams(
    pageNumber: number,
    pageSize: number,
    searchTerm?: string,
    isActive?: boolean,
    exerciseTypeId?: string,
  ): HttpParams {
    let params = new HttpParams()
      .set('pageNumber', Math.max(1, Math.floor(pageNumber)))
      .set('pageSize', Math.min(100, Math.max(1, Math.floor(pageSize))));
    const search = searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    if (exerciseTypeId) params = params.set('exerciseTypeId', exerciseTypeId);
    return params;
  }
}
