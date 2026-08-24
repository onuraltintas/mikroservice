import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GamificationService } from '../../../../core/services/gamification.service';
import { LeaderboardType, LeaderboardEntry } from '../../../../core/models/gamification.model';
import { AchievementBadgeComponent } from '../achievement-badge/achievement-badge.component';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    AchievementBadgeComponent
  ],
  templateUrl: './leaderboard.component.html',
  styleUrls: ['./leaderboard.component.scss']
})
export class LeaderboardComponent implements OnInit {
  leaderboardEntries: LeaderboardEntry[] = [];
  selectedType: LeaderboardType = LeaderboardType.TotalXP;
  isLoading: boolean = false;
  currentUserId?: string;

  leaderboardTypes = [
    { type: LeaderboardType.TotalXP, label: 'Toplam XP', icon: 'star' },
    { type: LeaderboardType.CurrentLevel, label: 'Seviye', icon: 'trending_up' },
    { type: LeaderboardType.CurrentStreak, label: 'Güncel Seri', icon: 'local_fire_department' },
    { type: LeaderboardType.LongestStreak, label: 'En Uzun Seri', icon: 'whatshot' },
    { type: LeaderboardType.TotalActivities, label: 'Aktivite', icon: 'assignment_turned_in' },
    { type: LeaderboardType.ReadingMinutes, label: 'Okuma Süresi', icon: 'schedule' }
  ];

  constructor(private gamificationService: GamificationService) {}

  ngOnInit(): void {
    this.loadLeaderboard();
    this.loadCurrentUser();
  }

  loadCurrentUser(): void {
    this.gamificationService.getUserGameification().subscribe({
      next: (gamification) => {
        this.currentUserId = gamification.userId;
      }
    });
  }

  loadLeaderboard(): void {
    this.isLoading = true;
    this.gamificationService.getLeaderboard(this.selectedType, 0, 50).subscribe({
      next: (entries) => {
        this.leaderboardEntries = entries;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading leaderboard:', error);
        this.isLoading = false;
      }
    });
  }

  selectType(type: LeaderboardType): void {
    this.selectedType = type;
    this.loadLeaderboard();
  }

  getRankIcon(rank: number): string {
    switch (rank) {
      case 1: return '🥇';
      case 2: return '🥈';
      case 3: return '🥉';
      default: return `#${rank}`;
    }
  }

  getRankClass(rank: number): string {
    switch (rank) {
      case 1: return 'gold';
      case 2: return 'silver';
      case 3: return 'bronze';
      default: return '';
    }
  }

  isCurrentUser(userId: string): boolean {
    return userId === this.currentUserId;
  }

  formatValue(value: number): string {
    const type = this.selectedType;

    switch (type) {
      case LeaderboardType.TotalXP:
        return this.formatXP(value);
      case LeaderboardType.CurrentLevel:
      case LeaderboardType.CurrentStreak:
      case LeaderboardType.LongestStreak:
      case LeaderboardType.TotalActivities:
        return value.toString();
      case LeaderboardType.ReadingMinutes:
        return this.formatMinutes(value);
      default:
        return value.toString();
    }
  }

  formatXP(xp: number): string {
    if (xp >= 1000000) return `${(xp / 1000000).toFixed(1)}M`;
    if (xp >= 1000) return `${(xp / 1000).toFixed(1)}K`;
    return xp.toString();
  }

  formatMinutes(minutes: number): string {
    if (minutes >= 60) {
      const hours = Math.floor(minutes / 60);
      return `${hours}h`;
    }
    return `${minutes}m`;
  }

  getValueUnit(): string {
    switch (this.selectedType) {
      case LeaderboardType.TotalXP:
        return 'XP';
      case LeaderboardType.CurrentLevel:
        return 'Seviye';
      case LeaderboardType.CurrentStreak:
      case LeaderboardType.LongestStreak:
        return 'Gün';
      case LeaderboardType.TotalActivities:
        return 'Aktivite';
      case LeaderboardType.ReadingMinutes:
        return 'Dakika';
      default:
        return '';
    }
  }

  getTierColor(level: number): string {
    return this.gamificationService.getTierColor(level);
  }
}
