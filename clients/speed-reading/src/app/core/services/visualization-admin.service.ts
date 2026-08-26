import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface VisualizationScene {
    id: string;
    description: string;
    duration: number;
    difficultyLevel: number;
    displayOrder: number;
    exerciseId: string;
    exerciseName?: string;
    questionCount: number;
    createdAt: Date;
    questions?: VisualizationQuestion[];
}

export interface VisualizationQuestion {
    id?: string;
    questionText: string;
    options: string[];
    correctAnswer: string;
    questionType: string;
    displayOrder: number;
}

export interface VisualizationListResponse {
    items: VisualizationScene[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
}

export interface ExerciseDropdown {
    id: string;
    title: string;
    difficultyLevel: number;
}

export interface ImportResult {
    successCount: number;
    failedCount: number;
    message: string;
    errors: string[];
}

@Injectable({
    providedIn: 'root'
})
export class VisualizationAdminService {
    private apiUrl = `${environment.speedReadingApiUrl}/admin/visualization-scenes`;

    constructor(private http: HttpClient) { }

    getScenes(
        pageNumber: number = 1,
        pageSize: number = 10,
        difficultyLevel?: number,
        searchTerm?: string
    ): Observable<VisualizationListResponse> {
        let params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());

        if (difficultyLevel) {
            params = params.set('difficultyLevel', difficultyLevel.toString());
        }
        if (searchTerm) {
            params = params.set('searchTerm', searchTerm);
        }

        return this.http.get<VisualizationListResponse>(this.apiUrl, { params });
    }

    getScene(id: string): Observable<VisualizationScene> {
        return this.http.get<any>(`${this.apiUrl}/${id}`).pipe(
            map((data: any) => {
                // Backend returns { scene: {...}, questions: [...] } after ApiResponse unwrap
                const scene = data.scene || data;
                const questions = (data.questions || scene.questions || []).map((q: any) => ({
                    ...q,
                    options: q.options || (q.optionsJson ? JSON.parse(q.optionsJson) : [])
                }));
                return { ...scene, questions } as VisualizationScene;
            })
        );
    }

    createScene(scene: Partial<VisualizationScene>): Observable<string> {
        return this.http.post<string>(this.apiUrl, scene);
    }

    updateScene(id: string, scene: Partial<VisualizationScene>): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${id}`, { ...scene, id });
    }

    deleteScene(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }

    getExercises(): Observable<ExerciseDropdown[]> {
        return this.http.get<ExerciseDropdown[]>(`${this.apiUrl}/exercises`);
    }

    importFromCsv(file: File): Observable<ImportResult> {
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post<ImportResult>(`${this.apiUrl}/import/csv`, formData);
    }

}
