import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { GamificationService } from '../../../../core/services/gamification.service';
import { UserGameification } from '../../../../core/models/gamification.model';
import { ProgressBarComponent } from '../../components/progress-bar/progress-bar.component';
import { StreakDisplayComponent } from '../../components/streak-display/streak-display.component';
import { StatsCardComponent } from '../../components/stats-card/stats-card.component';
import { AchievementGridComponent } from '../../components/achievement-grid/achievement-grid.component';
import { LeaderboardComponent } from '../../components/leaderboard/leaderboard.component';

@Component({
  selector: 'app-gamification-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    MatIconModule,
    ProgressBarComponent,
    StreakDisplayComponent,
    StatsCardComponent,
    AchievementGridComponent,
    LeaderboardComponent
  ],
  templateUrl: './gamification-dashboard.component.html',
  styleUrls: ['./gamification-dashboard.component.scss']
})
export class GamificationDashboardComponent implements OnInit {
  userGamification?: UserGameification;
  isLoading: boolean = true;

  constructor(private gamificationService: GamificationService) {}

  ngOnInit(): void {
    this.loadUserGamification();
  }

  loadUserGamification(): void {
    this.isLoading = true;
    this.gamificationService.getUserGameification().subscribe({
      next: (data) => {
        this.userGamification = data;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading gamification data:', error);
        this.isLoading = false;
      }
    });
  }

  getTierColor(level: number): string {
    return this.gamificationService.getTierColor(level);
  }

  formatDuration(minutes: number): string {
    return this.gamificationService.formatDuration(minutes);
  }
}
