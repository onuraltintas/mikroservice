import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Teacher, CreateTeacherRequest, UpdateTeacherRequest } from '../models/teacher.model';

/**
 * Teachers Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (Teacher[], Teacher, etc.)
 */
@Injectable({
  providedIn: 'root'
})
export class TeachersService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/teachers`;

  /**
   * Get all teachers with optional filters
   * Backend returns: ApiResponse<Teacher[]>
   * Service receives: Teacher[] (auto-unwrapped)
   */
  getTeachers(searchTerm?: string, institutionId?: string, isActive?: boolean): Observable<Teacher[]> {
    let params = new HttpParams();
    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }
    if (institutionId) {
      params = params.set('institutionId', institutionId);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    // Backend returns PagedResult<UserDto>; extract items array
    return this.http.get<any>(this.API_URL, { params }).pipe(
      map(result => Array.isArray(result) ? result : (result?.items ?? []))
    );
  }

  /**
   * Get single teacher by ID
   * Backend returns: ApiResponse<Teacher>
   * Service receives: Teacher (auto-unwrapped)
   */
  getTeacherById(id: string): Observable<Teacher> {
    return this.http.get<Teacher>(`${this.API_URL}/${id}`);
  }

  /**
   * Create new teacher
   * Backend returns: ApiResponse<Teacher>
   * Service receives: Teacher (auto-unwrapped)
   */
  createTeacher(request: CreateTeacherRequest): Observable<Teacher> {
    return this.http.post<Teacher>(this.API_URL, request);
  }

  /**
   * Update existing teacher
   * Backend returns: ApiResponse<Teacher>
   * Service receives: Teacher (auto-unwrapped)
   */
  updateTeacher(id: string, request: UpdateTeacherRequest): Observable<Teacher> {
    return this.http.put<Teacher>(`${this.API_URL}/${id}`, request);
  }

  /**
   * Delete teacher
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteTeacher(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  /**
   * Get students for a specific teacher (filtered by institution)
   * Backend returns: ApiResponse<User[]>
   * Service receives: User[] (auto-unwrapped)
   */
  getTeacherStudents(teacherId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.API_URL}/${teacherId}/students`);
  }

  /**
   * Get authenticated teacher's students (filtered by institution)
   * Backend returns: ApiResponse<User[]>
   * Service receives: User[] (auto-unwrapped)
   */
  getMyStudents(): Observable<any[]> {
    return this.http.get<any[]>(`${this.API_URL}/me/students`);
  }

  /**
   * Create new student for teacher's institution
   * Backend returns: ApiResponse<User>
   * Service receives: User (auto-unwrapped)
   */
  createStudent(request: CreateStudentRequest): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/students`, request);
  }

  /**
   * Update student details
   * Backend returns: ApiResponse<User>
   * Service receives: User (auto-unwrapped)
   */
  updateStudent(id: string, request: UpdateStudentRequest): Observable<any> {
    return this.http.put<any>(`${this.API_URL}/students/${id}`, request);
  }

  /**
   * Link existing student to teacher
   */
  linkStudent(email: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/students/link`, { studentEmail: email }, { headers: { 'X-Skip-Error-Toast': 'true' } });
  }

  /**
   * Unlink student from teacher (removes TeacherId, doesn't delete)
   */
  unlinkStudent(studentId: string): Observable<any> {
    return this.http.delete<any>(`${this.API_URL}/students/${studentId}/unlink`);
  }
  /**
   * Import students from Excel file
   * Backend returns: ApiResponse<ImportStudentsResultDto>
   * Service receives: ImportStudentsResultDto (auto-unwrapped)
   */
  importStudents(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.API_URL}/students/import`, formData);
  }

  /**
   * Download student import template
   */
  getImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.API_URL}/students/import/template`, { responseType: 'blob' });
  }
  /**
   * Import teachers from Excel file
   * Backend returns: ApiResponse<ImportTeachersResultDto>
   * Service receives: ImportTeachersResultDto (auto-unwrapped)
   */
  importTeachers(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.API_URL}/import`, formData);
  }

  /**
   * Download teacher import template
   */
  getTeacherImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.API_URL}/import/template`, { responseType: 'blob' });
  }
}

export interface CreateStudentRequest {
  firstName: string;
  lastName: string;
  email: string;
  password?: string;
}

export interface UpdateStudentRequest {
  firstName: string;
  lastName: string;
  email: string;
  password?: string;
}

export interface ImportStudentsResult {
  successCount: number;
  failureCount: number;
  errors: string[];
}
