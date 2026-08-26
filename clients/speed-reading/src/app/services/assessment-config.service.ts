import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface AssessmentTemplateDto {
    id: string;
    name: string;
    targetAgeGroupId: string;
    ageGroupName: string;
    ageGroupDisplayName: string;
    exercises: AssessmentExerciseConfigDto[];
    isActive: boolean;
    createdAt: Date;
}

export interface AssessmentExerciseConfigDto {
    exerciseId: string;
    exerciseTitle: string;
    exerciseType: string;
    difficultyLevel: number;
    customTitle?: string;
    customDescription?: string;
    displayOrder: number;
}

export interface CreateAssessmentTemplateCommand {
    name: string;
    targetAgeGroupId: string;
    exercises: AssessmentExerciseInput[];
}

export interface UpdateAssessmentTemplateCommand {
    name: string;
    exercises: AssessmentExerciseInput[];
}

export interface AssessmentExerciseInput {
    exerciseId: string;
    customTitle?: string;
    customDescription?: string;
    displayOrder: number;
}

export interface AgeGroupDto {
    id: string;
    name: string;
    displayName: string;
    minAge: number;
    maxAge?: number;
}

export interface ExerciseDto {
    id: string;
    title: string;
    exerciseType: string;
    difficultyLevel: number;
}

export interface ExerciseTypeDto {
    id: string;
    name: string;
    displayName: string;
}

interface ApiResponse<T> {
    success: boolean;
    data: T;
    message?: string;
}

@Injectable({
    providedIn: 'root'
})
export class AssessmentConfigService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.speedReadingApiUrl}/admin/assessment-templates`;

    getAllTemplates(): Observable<ApiResponse<AssessmentTemplateDto[]>> {
        return this.http.get<ApiResponse<AssessmentTemplateDto[]>>(this.apiUrl);
    }

    getTemplateByAgeGroup(ageGroupId: string): Observable<ApiResponse<AssessmentTemplateDto>> {
        return this.http.get<ApiResponse<AssessmentTemplateDto>>(`${this.apiUrl}/age-group/${ageGroupId}`);
    }

    createTemplate(command: CreateAssessmentTemplateCommand): Observable<ApiResponse<string>> {
        return this.http.post<ApiResponse<string>>(this.apiUrl, command);
    }

    updateTemplate(id: string, command: UpdateAssessmentTemplateCommand): Observable<ApiResponse<any>> {
        return this.http.put<ApiResponse<any>>(`${this.apiUrl}/${id}`, command);
    }

    deleteTemplate(id: string): Observable<ApiResponse<any>> {
        return this.http.delete<ApiResponse<any>>(`${this.apiUrl}/${id}`);
    }

    // Helper endpoints
    getAgeGroups(): Observable<ApiResponse<AgeGroupDto[]>> {
        return this.http.get<ApiResponse<AgeGroupDto[]>>(`${environment.speedReadingApiUrl}/age-group-configurations`);
    }

    getExerciseTypes(): Observable<any[]> {
        return this.http.get<any>(`${environment.speedReadingApiUrl}/exercise-types?pageSize=500`).pipe(
            map(result => Array.isArray(result) ? result : (result?.items ?? []))
        );
    }

    getExercises(difficultyLevel?: number, exerciseTypeId?: string, ageGroupId?: string): Observable<any[]> {
        const params: string[] = ['pageSize=500'];
        if (difficultyLevel) params.push(`difficultyLevel=${difficultyLevel}`);
        if (exerciseTypeId) params.push(`exerciseTypeId=${exerciseTypeId}`);
        if (ageGroupId) params.push(`targetAgeGroupId=${ageGroupId}`);
        const url = `${environment.speedReadingApiUrl}/exercises?${params.join('&')}`;
        return this.http.get<any>(url).pipe(
            map(result => Array.isArray(result) ? result : (result?.items ?? []))
        );
    }
}
