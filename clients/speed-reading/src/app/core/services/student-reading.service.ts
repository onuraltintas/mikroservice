import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ReadingText,
  ReadingQuestion,
  ReadingSession,
  ReadingSessionResult,
  StartReadingSessionDto,
  CompleteReadingSessionDto,
  ReadingStatistics
} from '../models/reading-text.model';

@Injectable({
  providedIn: 'root'
})
export class StudentReadingService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/student-reading`;

  // Get available categories for students
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/categories`);
  }

  // Get available texts for students
  getAvailableTexts(
    category?: string,
    minLevel?: number,
    maxLevel?: number
  ): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    if (minLevel !== undefined) params = params.set('minLevel', minLevel.toString());
    if (maxLevel !== undefined) params = params.set('maxLevel', maxLevel.toString());

    return this.http.get<ReadingText[]>(`${this.API_URL}/available`, { params });
  }

  // Get text details with questions - Start a reading session
  getTextWithQuestions(textId: string): Observable<ReadingText> {
    return this.http.get<ReadingText>(`${this.API_URL}/${textId}/start`);
  }

  // Get questions for a text (fallback - use start endpoint)
  getQuestions(textId: string): Observable<ReadingQuestion[]> {
    // Backend doesn't have separate /questions endpoint
    // Get full text with questions and extract questions
    return this.getTextWithQuestions(textId).pipe(
      map((text: ReadingText) => text.readingQuestions || [])
    );
  }

  // Start a reading session (alias for getTextWithQuestions for backward compatibility)
  startSession(textId: string): Observable<ReadingText> {
    return this.getTextWithQuestions(textId);
  }

  // Complete a reading session
  completeSession(textId: string, dto: CompleteReadingSessionDto): Observable<ReadingSessionResult> {
    return this.http.post<ReadingSessionResult>(`${this.API_URL}/${textId}/complete`, dto);
  }

  // Get reading history
  getHistory(
    startDate?: Date,
    endDate?: Date,
    category?: string
  ): Observable<ReadingSession[]> {
    let params = new HttpParams();
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());
    if (category) params = params.set('category', category);

    return this.http.get<ReadingSession[]>(`${this.API_URL}/history`, { params });
  }

  // Get session details
  getSessionDetails(sessionId: string): Observable<ReadingSessionResult> {
    return this.http.get<ReadingSessionResult>(`${this.API_URL}/sessions/${sessionId}`);
  }

  // Get statistics
  getStatistics(): Observable<ReadingStatistics> {
    return this.http.get<ReadingStatistics>(`${this.API_URL}/statistics`);
  }

  // Get WPM progression data
  getWPMProgression(limit: number = 10): Observable<{ date: Date; wpm: number }[]> {
    return this.http.get<{ date: Date; wpm: number }[]>(
      `${this.API_URL}/statistics/wpm-progression?limit=${limit}`
    );
  }

  // Get comprehension progression data
  getComprehensionProgression(limit: number = 10): Observable<{ date: Date; rate: number }[]> {
    return this.http.get<{ date: Date; rate: number }[]>(
      `${this.API_URL}/statistics/comprehension-progression?limit=${limit}`
    );
  }
}
