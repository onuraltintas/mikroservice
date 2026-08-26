import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { ExamQuestion, ExamType, QuestionCategory } from '../models/exam-question.model';

@Injectable({
    providedIn: 'root'
})
export class QuestionBankService {
    private apiUrl = `${environment.speedReadingApiUrl}/exam-questions`;

    constructor(private http: HttpClient) { }

    getQuestions(
        pageNumber: number = 1,
        pageSize: number = 10,
        examType?: ExamType,
        difficulty?: number,
        category?: QuestionCategory,
        searchTerm?: string
    ): Observable<PagedResult<ExamQuestion>> {
        let params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());

        if (examType !== undefined && examType !== null) {
            params = params.set('examType', examType.toString());
        }
        if (difficulty) {
            params = params.set('difficulty', difficulty.toString());
        }
        if (category !== undefined && category !== null) {
            params = params.set('category', category.toString());
        }
        if (searchTerm) {
            params = params.set('searchTerm', searchTerm);
        }

        return this.http.get<PagedResult<ExamQuestion>>(this.apiUrl, { params });
    }

    getQuestion(id: string): Observable<ExamQuestion> {
        return this.http.get<ExamQuestion>(`${this.apiUrl}/${id}`);
    }

    createQuestion(question: Partial<ExamQuestion>): Observable<string> {
        return this.http.post<string>(this.apiUrl, question);
    }

    updateQuestion(id: string, question: Partial<ExamQuestion>): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${id}`, question);
    }

    deleteQuestion(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }

    hardDeleteQuestion(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}/hard`);
    }
}
