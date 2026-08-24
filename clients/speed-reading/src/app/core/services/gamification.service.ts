import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  UserGameification,
  Achievement,
  UserAchievement,
  LevelUpResult,
  AchievementProgress,
  LeaderboardType,
  LeaderboardEntry
} from '../models/gamification.model';

@Injectable({
  providedIn: 'root'
})
export class GamificationService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/gamification`;

  // State management
  private userGamificationSubject = new BehaviorSubject<UserGameification | null>(null);
  public userGamification$ = this.userGamificationSubject.asObservable();

  private achievementsSubject = new BehaviorSubject<Achievement[]>([]);
  public achievements$ = this.achievementsSubject.asObservable();

  getUserGameification(): Observable<UserGameification> {
    return this.http.get<UserGameification>(`${this.apiUrl}/user`).pipe(
      tap(gamification => this.userGamificationSubject.next(gamification))
    );
  }

  awardXP(amount: number, source: string, sourceId?: string): Observable<LevelUpResult> {
    return this.http.post<LevelUpResult>(`${this.apiUrl}/award-xp`, {
      amount,
      source,
      sourceId
    }).pipe(
      tap(result => {
        // Update local state
        const current = this.userGamificationSubject.value;
        if (current) {
          current.totalXP = result.totalXP;
          current.currentLevel = result.newLevel || current.currentLevel;
          current.currentLevelXP = result.currentLevelXP;
          current.nextLevelXP = result.nextLevelXP;
          if (result.leveledUp) {
            current.levelTitle = result.levelTitle;
            current.levelIcon = result.levelIcon;
          }
          this.userGamificationSubject.next(current);
        }
      })
    );
  }

  getAllAchievements(): Observable<Achievement[]> {
    return this.http.get<Achievement[]>(`${this.apiUrl}/achievements`).pipe(
      tap(achievements => this.achievementsSubject.next(achievements))
    );
  }

  getUserAchievements(): Observable<UserAchievement[]> {
    return this.http.get<UserAchievement[]>(`${this.apiUrl}/achievements/user`);
  }

  checkAchievements(): Observable<Achievement[]> {
    return this.http.post<Achievement[]>(`${this.apiUrl}/achievements/check`, {});
  }

  updateShowcase(achievementIds: string[]): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.apiUrl}/achievements/showcase`, {
      achievementIds
    });
  }

  updateStreak(activityDate: Date, durationMinutes: number): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.apiUrl}/streak/update`, {
      activityDate,
      durationMinutes
    });
  }

  useStreakFreeze(): Observable<{ success: boolean }> {
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

  getTierColor(level: number): string {
    const tier = Math.ceil(level / 5);
    switch (tier) {
      case 1: return '#9E9E9E'; // Gri
      case 2: return '#4CAF50'; // Yeşil
      case 3: return '#2196F3'; // Mavi
      case 4: return '#FFD700'; // Altın
      default: return '#9E9E9E';
    }
  }

  getTierName(level: number): string {
    const tier = Math.ceil(level / 5);
    switch (tier) {
      case 1: return 'Başlangıç Okuyucu';
      case 2: return 'Gelişen Okuyucu';
      case 3: return 'İleri Okuyucu';
      case 4: return 'Master Okuyucu';
      default: return 'Okuyucu';
    }
  }

  getTierIcon(level: number): string {
    const tier = Math.ceil(level / 5);
    switch (tier) {
      case 1: return '📖';
      case 2: return '📗';
      case 3: return '📘';
      case 4: return '📕';
      default: return '📖';
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

  getStreakIcon(streak: number): string {
    if (streak >= 100) return '🔥🔥🔥🔥🔥';
    if (streak >= 30) return '🔥🔥🔥🔥';
    if (streak >= 14) return '🔥🔥🔥';
    if (streak >= 7) return '🔥🔥';
    if (streak >= 3) return '🔥';
    return '⚪';
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
