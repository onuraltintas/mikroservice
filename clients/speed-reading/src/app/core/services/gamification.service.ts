import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  UserGameification,
  Achievement,
  UserAchievement,
  LeaderboardType,
  LeaderboardEntry
} from '../models/gamification.model';

@Injectable({
  providedIn: 'root'
})
export class GamificationService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.speedReadingApiUrl}/gamification`;

  // State management
  private userGamificationSubject = new BehaviorSubject<UserGameification | null>(null);
  public userGamification$ = this.userGamificationSubject.asObservable();

  private achievementsSubject = new BehaviorSubject<Achievement[]>([]);
  public achievements$ = this.achievementsSubject.asObservable();

  /**
   * Live test 19.11.2026: the home page asked for the same gamification data two or three times because several
   * widgets load it together. Reads made within a few seconds share one request; a write clears the shared reads.
   */
  private static readonly ShareWindowMs = 10_000;
  private readonly sharedReads = new Map<string, { at: number; request: Observable<unknown> }>();

  private sharedRead<T>(key: string, create: () => Observable<T>, now = Date.now()): Observable<T> {
    const existing = this.sharedReads.get(key);
    if (existing && now - existing.at <= GamificationService.ShareWindowMs) return existing.request as Observable<T>;
    const request = create().pipe(shareReplay({ bufferSize: 1, refCount: false }));
    this.sharedReads.set(key, { at: now, request });
    request.subscribe({ error: () => this.sharedReads.delete(key) });
    return request;
  }

  /** Forget shared reads, e.g. after XP or achievements changed. */
  clearSharedReads(): void {
    this.sharedReads.clear();
  }

  getUserGameification(): Observable<UserGameification> {
    return this.sharedRead('user', () => this.http.get<UserGameification>(`${this.apiUrl}/user`).pipe(
      tap(gamification => this.userGamificationSubject.next(gamification))
    ));
  }

  getAllAchievements(): Observable<Achievement[]> {
    return this.sharedRead('achievements', () => this.http.get<Achievement[]>(`${this.apiUrl}/achievements`).pipe(
      tap(achievements => this.achievementsSubject.next(achievements))
    ));
  }

  getUserAchievements(): Observable<UserAchievement[]> {
    return this.sharedRead('achievements/user', () => this.http.get<UserAchievement[]>(`${this.apiUrl}/achievements/user`));
  }

  checkAchievements(): Observable<Achievement[]> {
    this.clearSharedReads();
    return this.http.post<Achievement[]>(`${this.apiUrl}/achievements/check`, {});
  }

  updateShowcase(achievementIds: string[]): Observable<{ success: boolean }> {
    this.clearSharedReads();
    return this.http.put<{ success: boolean }>(`${this.apiUrl}/achievements/showcase`, {
      achievementIds
    });
  }

  useStreakFreeze(): Observable<{ success: boolean }> {
    this.clearSharedReads();
    return this.http.post<{ success: boolean }>(`${this.apiUrl}/streak/use-freeze`, {});
  }

  getLeaderboard(type: LeaderboardType = LeaderboardType.TotalXP, skip: number = 0, take: number = 100): Observable<LeaderboardEntry[]> {
    return this.http.get<LeaderboardEntry[]>(`${this.apiUrl}/leaderboard`, {
      params: {
        type,
        skip: skip.toString(),
        take: take.toString()
      }
    });
  }

  // Helper methods
  calculateProgressPercentage(current: number, target: number): number {
    if (target === 0) return 0;
    return Math.min((current / target) * 100, 100);
  }

  /** H19: levels 1–5, 6–10, 11–15 and 16+ (as on the server); higher levels stay in the top tier. */
  private tierOf(level: number): number {
    return Math.min(4, Math.max(1, Math.ceil(level / 5)));
  }

  getTierColor(level: number): string {
    const tier = this.tierOf(level);
    switch (tier) {
      case 1: return '#9E9E9E'; // Gri
      case 2: return '#4CAF50'; // Yeşil
      case 3: return '#2196F3'; // Mavi
      case 4: return '#FFD700'; // Altın
      default: return '#9E9E9E';
    }
  }

  getTierName(level: number): string {
    const tier = this.tierOf(level);
    switch (tier) {
      case 1: return 'Başlangıç Okuyucu';
      case 2: return 'Gelişen Okuyucu';
      case 3: return 'İleri Okuyucu';
      case 4: return 'Master Okuyucu';
      default: return 'Okuyucu';
    }
  }

  getTierIcon(level: number): string {
    const tier = this.tierOf(level);
    switch (tier) {
      case 1: return 'menu_book';
      case 2: return 'auto_stories';
      case 3: return 'local_library';
      case 4: return 'workspace_premium';
      default: return 'menu_book';
    }
  }

  formatDuration(minutes: number): string {
    if (minutes < 60) {
      return `${minutes} dakika`;
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours} saat ${mins} dakika` : `${hours} saat`;
  }

  getTierText(tier: string): string {
    const tierMap: { [key: string]: string } = {
      'Bronze': 'Bronz',
      'Silver': 'Gümüş',
      'Gold': 'Altın',
      'Diamond': 'Elmas',
      'Special': 'Özel'
    };
    return tierMap[tier] || tier;
  }
}
