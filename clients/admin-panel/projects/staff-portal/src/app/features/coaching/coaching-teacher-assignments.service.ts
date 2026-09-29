import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherAssignment {
  id: string;
  title: string;
  type: string;
  dueDate: string;
  status: string;
  totalStudents: number;
  submittedCount: number;
  createdAt: string;
}

export interface PagedCoachingAssignments<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export type CoachingAssignmentStatus = 'Active' | 'Completed' | 'Cancelled';

@Injectable({ providedIn: 'root' })
export class CoachingTeacherAssignmentsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/assignments`;

  getTeacherAssignments(
    teacherId: string,
    pageNumber = 1,
    pageSize = 25,
    status?: CoachingAssignmentStatus
  ): Observable<PagedCoachingAssignments<CoachingTeacherAssignment>> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    if (status) params = params.set('status', status);

    return this.http.get<PagedCoachingAssignments<CoachingTeacherAssignment>>(
      `${this.url}/teacher/${encodeURIComponent(teacherId)}`,
      { params }
    );
  }

  cancelAssignment(assignmentId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.url}/${encodeURIComponent(assignmentId)}/cancel`,
      {}
    );
  }
}
