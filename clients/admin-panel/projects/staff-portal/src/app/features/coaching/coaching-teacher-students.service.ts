import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherStudent {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gradeLevel?: number;
  institutionName?: string;
  subject?: string;
  assignmentStartDate: string;
}

export interface PagedCoachingResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherStudentsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/teachers/me/students`;

  getMyStudents(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string
  ): Observable<PagedCoachingResponse<CoachingTeacherStudent>> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    const search = searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);

    return this.http.get<PagedCoachingResponse<CoachingTeacherStudent>>(this.url, { params });
  }
}
