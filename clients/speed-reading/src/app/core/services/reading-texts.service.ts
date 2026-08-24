import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ReadingText,
  ReadingQuestion,
  CreateReadingTextDto,
  UpdateReadingTextDto,
  CreateReadingQuestionDto,
  UpdateReadingQuestionDto,
  ImportReadingTextDto,
  ShortReadingText
} from '../models/reading-text.model';

@Injectable({
  providedIn: 'root'
})
export class ReadingTextsService {
  private readonly http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/reading-texts`;



  // ...

  // Admin method: Get all reading texts
  getAllTexts(
    category?: string,
    level?: number,
    ageGroupId?: string,
    isActive?: boolean,
    searchTerm?: string
  ): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    if (level !== undefined) params = params.set('difficultyLevel', level.toString());
    if (ageGroupId) params = params.set('ageGroupId', ageGroupId);
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (searchTerm) params = params.set('searchTerm', searchTerm);

    return this.http.get<ReadingText[]>(`${this.apiUrl}/exercise`, { params });
  }

  // Get distinct levels from database
  getLevels(): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/levels`);
  }

  // Get active age groups
  getAgeGroups(): Observable<{ id: string, name: string, displayName: string }[]> {
    return this.http.get<{ id: string, name: string, displayName: string }[]>(`${environment.apiUrl}/v1/age-group-configurations/active`);
  }

  // Simplified method for compatibility - Uses exercise endpoint for students
  getReadingTexts(difficultyLevel?: number, onlyWithQuestions: boolean = false): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (difficultyLevel !== undefined) {
      params = params.set('difficultyLevel', difficultyLevel.toString());
    }
    if (onlyWithQuestions) {
      params = params.set('onlyWithQuestions', 'true');
    }
    return this.http.get<ReadingText[]>(`${this.apiUrl}/exercise`, { params });
  }

  getTextById(id: string): Observable<ReadingText> {
    return this.http.get<ReadingText>(`${this.apiUrl}/exercise/${id}`);
  }

  // Admin methods
  createText(dto: CreateReadingTextDto): Observable<ReadingText> {
    return this.http.post<ReadingText>(`${this.apiUrl}/exercise`, dto);
  }

  updateText(id: string, dto: UpdateReadingTextDto): Observable<ReadingText> {
    return this.http.put<ReadingText>(`${this.apiUrl}/exercise/${id}`, dto);
  }

  deleteText(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/exercise/${id}`);
  }

  // Question Management
  getTextWithQuestions(readingTextId: string): Observable<ReadingText> {
    return this.http.get<ReadingText>(`${this.apiUrl}/${readingTextId}/with-questions`);
  }

  createQuestion(dto: CreateReadingQuestionDto): Observable<ReadingQuestion> {
    return this.http.post<ReadingQuestion>(`${this.apiUrl}/${dto.readingTextId}/questions`, dto);
  }

  updateQuestion(questionId: string, dto: UpdateReadingQuestionDto): Observable<ReadingQuestion> {
    return this.http.put<ReadingQuestion>(`${this.apiUrl}/questions/${questionId}`, dto);
  }

  deleteQuestion(questionId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/questions/${questionId}`);
  }

  // Get categories from database
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/categories`);
  }

  // Update reading text active/inactive status
  updateStatus(id: string, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/status`, { isActive });
  }

  // Get short texts for RSVP practice (under 200 words)
  getShortTexts(limit: number = 10): Observable<ShortReadingText[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<ShortReadingText[]>(`${this.apiUrl}/short`, { params });
  }

  // Import functionality
  importFromCsv(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.apiUrl}/import/csv`, formData);
  }

  importFromExcel(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.apiUrl}/import/excel`, formData);
  }

  importBulk(texts: ImportReadingTextDto[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/import/bulk`, texts);
  }

  // Export functionality
  exportToPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/export/pdf`, {
      responseType: 'blob'
    });
  }

  exportToDocx(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/export/docx`, {
      responseType: 'blob'
    });
  }

  exportMultipleToPdf(ids: string[]): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/pdf`, { ids }, {
      responseType: 'blob'
    });
  }

  exportMultipleToDocx(ids: string[]): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/docx`, { ids }, {
      responseType: 'blob'
    });
  }

  // Helper method for downloading files
  downloadFile(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
