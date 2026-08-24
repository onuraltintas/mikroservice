import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface StudentLearningProfile {
  id: string;
  studentId: string;
  currentDifficultyLevel: number;
  averageWPM: number;
  averageComprehension: number;
  totalReadingSessions: number;
  totalExerciseSessions: number;
  currentStreak: number;
  longestStreak: number;
  lastActiveDate: string;
  bloomPerformance: { [key: number]: number };
  categoryPreferences: { [key: string]: number };
  weakAreas: number[];
  totalMinutesSpent: number;
  createdAt: string;
  updatedAt: string;
}

export interface WeakArea {
  bloomLevel: number;
  bloomLevelName: string;
  currentScore: number;
  recommendation: string;
}

export interface ContentRecommendation {
  id: string;
  readingTextId: string;
  title: string;
  category: string;
  difficultyLevel: number;
  wordCount: number;
  recommendationScore: number;
  reason: string;
  recommendationType: string;
  isViewed: boolean;
  isStarted: boolean;
  isCompleted: boolean;
  createdAt: string;
  expiresAt: string;
}

export interface DailyGoal {
  id: string;
  studentId: string;
  date: string;
  targetMinutes: number;
  targetReadingSessions: number;
  targetExercises: number;
  actualMinutes: number;
  actualReadingSessions: number;
  actualExercises: number;
  isAchieved: boolean;
  achievedAt: string | null;
  achievementPercentage: number;
  motivationalMessage: string;
  createdAt: string;
  updatedAt: string;
}

export interface AdaptiveDashboard {
  profile: StudentLearningProfile;
  todayGoal: DailyGoal;
  weakAreas: WeakArea[];
  recommendations: ContentRecommendation[];
  currentStreak: number;
  totalMinutes: number;
  totalSessions: number;
  overallProgress: number;
}

export interface UpdateAfterSessionRequest {
  readingTextId: string;
  timeSpentSeconds: number;
  wpm: number;
  comprehensionScore: number;
  bloomAnswers: { [key: number]: boolean };
}

export interface UpdateDailyProgressRequest {
  minutesSpent: number;
  sessionCompleted: boolean;
  exerciseCompleted: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AdaptiveLearningService {
  private apiUrl = `${environment.apiUrl}/v1/adaptive-learning`;

  constructor(private http: HttpClient) {}

  getProfile(): Observable<StudentLearningProfile> {
    return this.http.get<StudentLearningProfile>(`${this.apiUrl}/profile`);
  }

  getDashboard(): Observable<AdaptiveDashboard> {
    return this.http.get<AdaptiveDashboard>(`${this.apiUrl}/dashboard`);
  }

  getWeakAreas(): Observable<WeakArea[]> {
    const bloomNames: Record<number, string> = { 1: 'Hatırlama', 2: 'Anlama', 3: 'Uygulama', 4: 'Analiz', 5: 'Değerlendirme', 6: 'Yaratma' };
    return this.http.get<any>(`${this.apiUrl}/weak-areas`).pipe(
      map((r: any) => {
        const levels: number[] = Array.isArray(r) ? r : (Array.isArray(r?.weakAreas) ? r.weakAreas : []);
        return levels.map(level => ({
          bloomLevel: level,
          bloomLevelName: bloomNames[level] ?? `Seviye ${level}`,
          currentScore: 0,
          recommendation: ''
        }));
      })
    );
  }

  getRecommendations(count: number = 5): Observable<ContentRecommendation[]> {
    return this.http.get<ContentRecommendation[]>(`${this.apiUrl}/recommendations?count=${count}`);
  }

  getDailyGoal(): Observable<DailyGoal> {
    return this.http.get<DailyGoal>(`${this.apiUrl}/daily-goal`);
  }

  updateAfterSession(request: UpdateAfterSessionRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/update-after-session`, request);
  }

  updateDailyProgress(request: UpdateDailyProgressRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/update-daily-progress`, request);
  }
}
