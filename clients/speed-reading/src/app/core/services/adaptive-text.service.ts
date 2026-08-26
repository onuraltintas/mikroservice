import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  AdaptiveTextRecommendation,
  StudentReadingProfile,
  UpdateProfileRequest,
  RecordRecommendationRequest,
  SelectionCriteria
} from '../models/adaptive-text.model';

/**
 * Service for adaptive text selection and student reading profile management
 */
@Injectable({
  providedIn: 'root'
})
export class AdaptiveTextService {
  private readonly apiUrl = `${environment.speedReadingApiUrl}/adaptive-texts`;

  // Cache student profile for performance
  private profileCache$ = new BehaviorSubject<StudentReadingProfile | null>(null);
  public currentProfile$ = this.profileCache$.asObservable();

  constructor(private http: HttpClient) { }

  /**
   * Get top N adaptive text recommendations for a student
   */
  getRecommendations(
    studentId: string,
    topN: number = 5,
    trainingSeriesId?: string,
    selectionCriteria?: SelectionCriteria
  ): Observable<AdaptiveTextRecommendation[]> {
    let params = new HttpParams().set('count', topN.toString());

    if (trainingSeriesId) {
      params = params.set('trainingSeriesId', trainingSeriesId);
    }

    if (selectionCriteria) {
      params = params.set('selectionCriteria', JSON.stringify(selectionCriteria));
    }

    return this.http.get<AdaptiveTextRecommendation[]>(
      `${this.apiUrl}/recommendations/${studentId}`,
      { params }
    );
  }

  /**
   * Get the best single text recommendation for a student
   */
  getBestMatch(
    studentId: string,
    trainingSeriesId?: string,
    selectionCriteria?: SelectionCriteria
  ): Observable<AdaptiveTextRecommendation> {
    let params = new HttpParams();

    if (trainingSeriesId) {
      params = params.set('trainingSeriesId', trainingSeriesId);
    }

    if (selectionCriteria) {
      params = params.set('selectionCriteria', JSON.stringify(selectionCriteria));
    }

    return this.http.get<AdaptiveTextRecommendation>(
      `${this.apiUrl}/best-match/${studentId}`,
      { params }
    );
  }

  /**
   * Get student reading profile
   */
  getProfile(studentId: string, forceRefresh: boolean = false): Observable<StudentReadingProfile> {
    // Return cached profile if available and not forcing refresh
    if (!forceRefresh && this.profileCache$.value?.studentId === studentId) {
      return this.currentProfile$ as Observable<StudentReadingProfile>;
    }

    return this.http.get<StudentReadingProfile>(`${this.apiUrl}/profile/${studentId}`).pipe(
      tap(profile => {
        // Parse date string to Date object
        profile.lastCalculatedAt = new Date(profile.lastCalculatedAt);
        this.profileCache$.next(profile);
      })
    );
  }

  /**
   * Update student profile after completing a reading session
   */
  updateProfile(request: UpdateProfileRequest): Observable<StudentReadingProfile> {
    return this.http.post<StudentReadingProfile>(`${this.apiUrl}/update-profile`, request).pipe(
      tap(profile => {
        profile.lastCalculatedAt = new Date(profile.lastCalculatedAt);
        this.profileCache$.next(profile);
      })
    );
  }

  /**
   * Record that a recommendation was accepted (student started reading)
   */
  recordRecommendation(request: RecordRecommendationRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/record-recommendation`, request);
  }

  /**
   * Clear cached profile
   */
  clearProfileCache(): void {
    this.profileCache$.next(null);
  }

  /**
   * Get level description for display
   */
  getLevelDescription(level: number): string {
    if (level <= 2) return 'Başlangıç';
    if (level <= 4) return 'Temel';
    if (level <= 6) return 'Orta';
    if (level <= 8) return 'İleri';
    return 'Uzman';
  }

  /**
   * Get level color for UI
   */
  getLevelColor(level: number): string {
    if (level <= 2) return 'primary';
    if (level <= 4) return 'accent';
    if (level <= 6) return 'warn';
    if (level <= 8) return 'success';
    return 'purple';
  }

  /**
   * Format reading time for display
   */
  formatReadingTime(seconds: number): string {
    if (seconds < 60) return `${seconds} saniye`;
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    if (remainingSeconds === 0) return `${minutes} dakika`;
    return `${minutes} dk ${remainingSeconds} sn`;
  }

  /**
   * Calculate reading speed description
   */
  getSpeedDescription(wpm: number): string {
    if (wpm === 0) return 'Henüz veri yok';
    if (wpm < 150) return 'Yavaş';
    if (wpm < 250) return 'Ortalama';
    if (wpm < 350) return 'Hızlı';
    if (wpm < 500) return 'Çok Hızlı';
    return 'Süper Hızlı';
  }

  /**
   * Get score breakdown for charts
   */
  getScoreBreakdownData(scoreBreakdown: { [key: string]: number }): any[] {
    return Object.entries(scoreBreakdown).map(([name, value]) => ({
      name: this.translateScoreName(name),
      value: Math.round(value * 10) / 10 // Round to 1 decimal
    }));
  }

  /**
   * Translate score breakdown names to Turkish
   */
  private translateScoreName(name: string): string {
    const translations: { [key: string]: string } = {
      'LevelMatch': 'Seviye Uyumu',
      'CategoryPreference': 'Kategori Tercihi',
      'Freshness': 'Yenilik',
      'SuccessRate': 'Başarı Oranı',
      'WordCount': 'Kelime Sayısı'
    };
    return translations[name] || name;
  }

  /**
   * Get confidence level description
   */
  getConfidenceDescription(confidence: number): string {
    if (confidence >= 0.8) return 'Yüksek Güven';
    if (confidence >= 0.6) return 'Orta Güven';
    if (confidence >= 0.4) return 'Düşük Güven';
    return 'Çok Düşük Güven';
  }

  /**
   * Get confidence color
   */
  getConfidenceColor(confidence: number): string {
    if (confidence >= 0.8) return 'success';
    if (confidence >= 0.6) return 'primary';
    if (confidence >= 0.4) return 'warn';
    return 'danger';
  }
}
