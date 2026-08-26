import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  SaveFeedbackRequest,
  FeedbackAnalytics,
  RecommendedContent,
  WeakArea
} from '../models/content-feedback.model';

@Injectable({
  providedIn: 'root'
})
export class ContentFeedbackService {
  private apiUrl = `${environment.speedReadingApiUrl}/content-feedback`;

  constructor(private http: HttpClient) { }

  /**
   * Kullanıcı feedback'ini kaydet
   */
  saveFeedback(request: SaveFeedbackRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}`, request);
  }

  /**
   * Kullanıcının feedback analytics'lerini getir
   */
  getAnalytics(contentType?: string): Observable<FeedbackAnalytics> {
    let params = new HttpParams();
    if (contentType) {
      params = params.set('contentType', contentType);
    }
    return this.http.get<FeedbackAnalytics>(`${this.apiUrl}/analytics`, { params });
  }

  /**
   * Skorlanmış önerileri getir
   */
  getRecommendedContents(
    contentType: string,
    limit: number = 10
  ): Observable<RecommendedContent[]> {
    const params = new HttpParams()
      .set('contentType', contentType)
      .set('limit', limit.toString());

    return this.http.get<RecommendedContent[]>(`${this.apiUrl}/recommended`, { params });
  }

  /**
   * Optimal çalışma saatlerini getir
   */
  getOptimalStudyHours(): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/optimal-hours`);
  }

  /**
   * Tekrar edilmesi gereken içerikleri getir
   */
  getContentsNeedingRetry(contentType: string): Observable<string[]> {
    const params = new HttpParams().set('contentType', contentType);
    return this.http.get<string[]>(`${this.apiUrl}/retry-needed`, { params });
  }

  /**
   * Device type'ı tespit et
   */
  getDeviceType(): string {
    const userAgent = navigator.userAgent.toLowerCase();
    if (/(tablet|ipad|playbook|silk)|(android(?!.*mobi))/i.test(userAgent)) {
      return 'Tablet';
    }
    if (/Mobile|Android|iP(hone|od)|IEMobile|BlackBerry|Kindle|Silk-Accelerated|(hpw|web)OS|Opera M(obi|ini)/.test(userAgent)) {
      return 'Mobile';
    }
    return 'Desktop';
  }

  /**
   * Expected time hesapla (kelime sayısına göre)
   */
  calculateExpectedTime(wordCount: number, targetWPM: number = 200): number {
    return Math.ceil((wordCount / targetWPM) * 60); // saniye cinsinden
  }

  /**
   * Hızlı feedback kaydetme (reading için)
   */
  saveReadingFeedback(
    textId: string,
    wordCount: number,
    timeSpentSeconds: number,
    comprehensionScore: number,
    interactionCount: number,
    pauseCount: number,
    category?: string,
    difficultyLevel?: number,
    isLiked: boolean = false,
    isBookmarked: boolean = false
  ): Observable<any> {
    const expectedTime = this.calculateExpectedTime(wordCount);
    const completionRate = Math.min((timeSpentSeconds / expectedTime) * 100, 100);

    const request: SaveFeedbackRequest = {
      contentId: textId,
      contentType: 'ReadingText',
      isLiked,
      isBookmarked,
      completionRate,
      timeSpentSeconds,
      expectedTimeSeconds: expectedTime,
      comprehensionScore,
      retryCount: 0,
      interactionCount,
      pauseCount,
      deviceType: this.getDeviceType(),
      contentCategory: category,
      contentDifficultyLevel: difficultyLevel
    };

    return this.saveFeedback(request);
  }

  /**
   * Hızlı feedback kaydetme (exercise için)
   */
  saveExerciseFeedback(
    exerciseId: string,
    timeSpentSeconds: number,
    exerciseScore: number,
    interactionCount: number,
    pauseCount: number,
    exerciseType?: string,
    difficultyLevel?: number,
    isLiked: boolean = false,
    isBookmarked: boolean = false,
    abandonedAtPercentage?: number
  ): Observable<any> {
    const request: SaveFeedbackRequest = {
      contentId: exerciseId,
      contentType: 'Exercise',
      isLiked,
      isBookmarked,
      completionRate: abandonedAtPercentage ? 100 - abandonedAtPercentage : 100,
      timeSpentSeconds,
      expectedTimeSeconds: timeSpentSeconds, // Exercise için expected = actual
      exerciseScore,
      retryCount: 0,
      interactionCount,
      pauseCount,
      abandonedAtPercentage,
      deviceType: this.getDeviceType(),
      contentCategory: exerciseType,
      contentDifficultyLevel: difficultyLevel
    };

    return this.saveFeedback(request);
  }

  /**
   * Content skip feedback kaydet
   */
  saveSkipFeedback(
    contentId: string,
    contentType: 'ReadingText' | 'Exercise' | 'ProgramTemplate', // Changed: TrainingSeries → ProgramTemplate
    skipReason: string,
    timeSpentSeconds: number,
    abandonedAtPercentage: number
  ): Observable<any> {
    const request: SaveFeedbackRequest = {
      contentId,
      contentType,
      skipReason,
      isLiked: false,
      isBookmarked: false,
      completionRate: 100 - abandonedAtPercentage,
      timeSpentSeconds,
      expectedTimeSeconds: timeSpentSeconds * 2, // Tahmin
      retryCount: 0,
      interactionCount: 0,
      pauseCount: 0,
      abandonedAtPercentage,
      deviceType: this.getDeviceType()
    };

    return this.saveFeedback(request);
  }

  /**
   * Zayıf alanları getir
   */
  getWeakAreas(): Observable<WeakArea[]> {
    return this.getAnalytics().pipe(
      map(analytics => analytics.weakAreas || [])
    );
  }

  /**
   * Explicit feedback güncelle (rating, like, bookmark)
   */
  updateExplicitFeedback(
    contentId: string,
    contentType: string,
    data: {
      rating?: number;
      isLiked?: boolean;
      isBookmarked?: boolean;
    }
  ): Observable<any> {
    return this.http.patch(`${this.apiUrl}/${contentId}/${contentType}`, data);
  }
}
