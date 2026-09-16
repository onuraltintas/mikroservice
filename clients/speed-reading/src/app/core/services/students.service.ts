import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  Student,
  CreateStudentRequest,
  UpdateStudentRequest,
  StudentExerciseResult,
  StudentDashboard,
  ReadingText,
  Exercise,
  LevelUpResult
} from '../models/student.model';
import { PagedResult } from '../models/user.model';

/**
 * Students Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls now work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Services receive clean typed data (Student[], Student, etc.)
 * - Error handling delegated to GlobalErrorHandler
 */
@Injectable({
  providedIn: 'root'
})
export class StudentsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/students`;
  private readonly identityApiUrl = environment.apiUrl;

  /**
   * Get all students with optional filters
   * Backend returns: ApiResponse<Student[]>
   * Service receives: Student[] (auto-unwrapped by interceptor)
   */
  getStudents(
    searchTerm?: string,
    _institutionId?: string,
    _currentLevel?: number,
    isActive?: boolean,
    _teacherId?: string
  ): Observable<Student[]> {
    let params = new HttpParams().set('pageSize', '100').set('role', 'Student');

    if (searchTerm) {
      params = params.set('search', searchTerm);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<any>(`${this.identityApiUrl}/users`, { params }).pipe(
      map(result => (Array.isArray(result) ? result : (result?.items ?? [])).map((student: any) => this.toStudent(student)))
    );
  }

  getInstitutionStudents(
    searchTerm?: string,
    gradeLevel?: number,
    isActive?: boolean,
    teacherUserId?: string
  ): Observable<Student[]> {
    return this.getInstitutionStudentsPage(1, 100, searchTerm, gradeLevel, isActive, teacherUserId)
      .pipe(map(page => page.items));
  }

  getInstitutionStudentsPage(
    page = 1,
    pageSize = 25,
    searchTerm?: string,
    gradeLevel?: number,
    isActive?: boolean,
    teacherUserId?: string
  ): Observable<PagedResult<Student>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (searchTerm) params = params.set('search', searchTerm);
    if (gradeLevel !== undefined) params = params.set('gradeLevel', gradeLevel.toString());
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (teacherUserId) params = params.set('teacherUserId', teacherUserId);

    return this.http.get<any>(`${this.identityApiUrl}/institution/students`, { params }).pipe(
      map(result => {
        const rows = Array.isArray(result) ? result : (result?.items ?? []);
        const resultPage = result?.pageNumber ?? page;
        const resultSize = result?.pageSize ?? pageSize;
        const totalCount = result?.totalCount ?? rows.length;
        return {
          items: rows.map((student: any) => this.toStudent(student)),
          totalCount,
          pageNumber: resultPage,
          pageSize: resultSize,
          totalPages: Math.max(1, Math.ceil(totalCount / resultSize)),
          hasPreviousPage: resultPage > 1,
          hasNextPage: resultPage < Math.ceil(totalCount / resultSize)
        };
      })
    );
  }

  /**
   * Get single student by ID
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  getStudentById(id: string): Observable<Student> {
    return this.http.get<Student>(`${this.API_URL}/${id}`);
  }

  /**
   * Create new student
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  createStudent(request: CreateStudentRequest): Observable<Student> {
    return this.http.post<Student>(`${this.identityApiUrl}/institution/students`, request);
  }

  /**
   * Update existing student
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  updateStudent(id: string, request: UpdateStudentRequest): Observable<Student> {
    return this.http.put<Student>(`${this.identityApiUrl}/institution/students/${id}`, request);
  }

  /**
   * Delete student
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteStudent(id: string): Observable<void> {
    return this.http.delete<void>(`${this.identityApiUrl}/institution/students/${id}`);
  }

  /**
   * Unlink student from institution (Admin only)
   * Backend returns: ApiResponse<void>
   */
  unlinkStudentFromInstitution(id: string): Observable<void> {
    return this.deleteStudent(id);
  }

  /**
   * Get student exercise results (for teachers)
   * Backend returns: ApiResponse<StudentExerciseResult[]>
   * Service receives: StudentExerciseResult[] (auto-unwrapped)
   */
  getStudentExerciseResults(teacherId: string, studentId: string): Observable<StudentExerciseResult[]> {
    return this.http.get<StudentExerciseResult[]>(
      `${environment.apiUrl}/v1/teachers/${teacherId}/students/${studentId}/results`
    );
  }

  /**
   * Get student results (generic/admin access)
   * Backend returns: ApiResponse<StudentExerciseResult[]>
   */
  getStudentResults(studentId: string): Observable<StudentExerciseResult[]> {
    return this.http.get<StudentExerciseResult[]>(
      `${this.API_URL}/${studentId}/results`
    );
  }

  /**
   * Get comprehensive student dashboard
   * Backend returns: ApiResponse<StudentDashboard>
   * Service receives: StudentDashboard (auto-unwrapped)
   */
  getDashboard(): Observable<StudentDashboard> {
    return this.http.get<StudentDashboard>(`${this.API_URL}/dashboard`);
  }

  /**
   * Get recommended reading texts based on student profile
   * Backend returns: ApiResponse<ReadingText[]>
   * Service receives: ReadingText[] (auto-unwrapped)
   */
  getRecommendedReadingTexts(limit?: number): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (limit) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<ReadingText[]>(`${this.API_URL}/recommended-texts`, { params });
  }

  /**
   * Get recommended exercises based on student profile
   * Backend returns: ApiResponse<Exercise[]>
   * Service receives: Exercise[] (auto-unwrapped)
   */
  getRecommendedExercises(limit?: number): Observable<Exercise[]> {
    let params = new HttpParams();
    if (limit) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<Exercise[]>(`${this.API_URL}/recommended-exercises`, { params });
  }

  /**
   * Check if student is eligible to level up
   * Backend returns: ApiResponse<LevelUpResult>
   * Service receives: LevelUpResult (auto-unwrapped)
   */
  checkLevelUpEligibility(autoLevelUp: boolean = false): Observable<LevelUpResult> {
    let params = new HttpParams();
    if (autoLevelUp) {
      params = params.set('autoLevelUp', 'true');
    }
    return this.http.post<LevelUpResult>(`${this.API_URL}/check-level-up`, {}, { params });
  }

  /**
   * Get student's reading baseline from history
   * Backend returns: ApiResponse<ReadingBaselineDto>
   * Service receives: ReadingBaselineDto (auto-unwrapped)
   */
  getReadingBaseline(studentId: string, sessionCount: number = 10): Observable<import('../models/reading-baseline.model').ReadingBaselineDto> {
    let params = new HttpParams();
    params = params.set('sessionCount', sessionCount.toString());

    return this.http.get<import('../models/reading-baseline.model').ReadingBaselineDto>(
      `${this.API_URL}/${studentId}/reading-baseline`,
      { params }
    );
  }

  linkStudent(email: string, teacherId?: string | null): Observable<any> {
    return this.http.post<any>(`${this.identityApiUrl}/institution/invite-student`, {
      studentEmail: email,
      teacherUserId: teacherId
    });
  }

  private toStudent(student: any): Student {
    return {
      id: student.id ?? student.userId,
      firstName: student.firstName ?? '',
      lastName: student.lastName ?? '',
      email: student.email ?? '',
      institutionId: student.institutionId ?? student.studentDetails?.institutionId ?? undefined,
      institutionName: student.institutionName ?? student.studentDetails?.institutionName ?? undefined,
      currentLevel: student.gradeLevel ?? student.currentLevel ?? student.studentDetails?.gradeLevel ?? 0,
      targetWPM: student.targetWPM ?? student.studentDetails?.targetWPM ?? null,
      targetComprehension: student.targetComprehension ?? student.studentDetails?.targetComprehension ?? null,
      dailyGoalMinutes: student.dailyGoalMinutes ?? student.studentDetails?.dailyGoalMinutes ?? null,
      learningStyle: student.learningStyle ?? student.studentDetails?.learningStyle ?? 'Belirtilmedi',
      lastLoginAt: student.lastLoginAt ? new Date(student.lastLoginAt) : undefined,
      isActive: student.isActive ?? true,
      createdAt: student.createdAt ? new Date(student.createdAt) : new Date(),
      teacherId: student.teacherUserId ?? student.teacherId ?? undefined,
      teacherName: student.teacherName ?? undefined
    };
  }
}
