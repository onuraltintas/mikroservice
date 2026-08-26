import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';

export interface CreateAssignmentRequest {
    exerciseId: string;
    readingTextId?: string | null;
    studentIds: string[];
    title: string;
    description: string;
    dueDate: string; // ISO Date
}

export interface AssignmentDto {
    id: string; // Assignment Id
    studentAssignmentId: string; // The one we track
    title: string;
    description: string;
    exerciseTitle: string;
    exerciseId: string;
    dueDate: string;
    isCompleted: boolean;
    score?: number;
    completionDate?: string;
}

export interface TeacherAssignmentDto {
    id: string;
    title: string;
    description: string;
    exerciseTitle: string;
    exerciseTypeName: string;
    readingTextTitle?: string;
    dueDate: string;
    createdAt: string;
    studentCount: number;
    completedCount: number;
    isActive: boolean;
}

export interface AssignmentStudentDto {
    studentId: string;
    firstName: string;
    lastName: string;
    fullName: string;
    isCompleted: boolean;
    completionDate?: string;
    score?: number;
}

export interface AssignmentDetailDto {
    id: string;
    title: string;
    description: string;
    exerciseTitle: string;
    dueDate: string;
    isActive: boolean;
    createdAt: string;
    teacherId: string;
    students: AssignmentStudentDto[];
}

@Injectable({
    providedIn: 'root'
})
export class AssignmentService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.speedReadingApiUrl}/assignments`;

    createAssignment(request: CreateAssignmentRequest): Observable<string> {
        return this.http.post<string>(this.apiUrl, request);
    }

    getMyAssignments(): Observable<AssignmentDto[]> {
        return this.http.get<AssignmentDto[]>(`${this.apiUrl}/my-assignments`).pipe(
            map(items => items.map(item => ({
                ...item,
                studentAssignmentId: item.studentAssignmentId || item.id
            })))
        );
    }

    getTeacherAssignments(pageNumber = 1, pageSize = 10, searchTerm?: string, isActive?: boolean, exerciseTypeId?: string): Observable<PagedResult<TeacherAssignmentDto>> {
        let params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());

        if (searchTerm) params = params.set('searchTerm', searchTerm);
        if (isActive !== undefined && isActive !== null) params = params.set('isActive', isActive.toString());
        if (exerciseTypeId) params = params.set('exerciseTypeId', exerciseTypeId);

        return this.http.get<PagedResult<TeacherAssignmentDto>>(`${this.apiUrl}/teacher-assignments`, { params });
    }

    deleteAssignment(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }

    getAssignmentDetails(id: string): Observable<AssignmentDetailDto> {
        return this.http.get<AssignmentDetailDto>(`${this.apiUrl}/${id}/details`);
    }

    addStudentToAssignment(assignmentId: string, studentId: string): Observable<void> {
        return this.http.post<void>(`${this.apiUrl}/${assignmentId}/students`, { assignmentId, studentId });
    }

    removeStudentFromAssignment(assignmentId: string, studentId: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${assignmentId}/students/${studentId}`);
    }
}
