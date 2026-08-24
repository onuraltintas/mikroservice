import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';
import { Achievement, LevelUpResult } from '../../../core/models/gamification.model';
import { AchievementNotificationComponent } from '../components/achievement-notification/achievement-notification.component';
import { MatDialog } from '@angular/material/dialog';
import { LevelUpModalComponent } from '../components/level-up-modal/level-up-modal.component';

@Injectable({
  providedIn: 'root'
})
export class GamificationNotificationService {
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  // Achievement unlocked notification
  showAchievementUnlocked(achievement: Achievement): void {
    const config: MatSnackBarConfig = {
      duration: 5000,
      horizontalPosition: 'end',
      verticalPosition: 'top',
      panelClass: ['achievement-snackbar', 'success']
    };

    this.snackBar.openFromComponent(AchievementNotificationComponent, {
      ...config,
      data: {
        type: 'achievement',
        achievement,
        title: 'Yeni Başarı Kazanıldı! 🎉',
        message: achievement.name
      }
    });
  }

  // Multiple achievements unlocked
  showAchievementsUnlocked(achievements: Achievement[]): void {
    achievements.forEach((achievement, index) => {
      setTimeout(() => {
        this.showAchievementUnlocked(achievement);
      }, index * 500); // Stagger notifications
    });
  }

  // XP gained notification
  showXPGained(amount: number): void {
    const config: MatSnackBarConfig = {
      duration: 2000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['xp-snackbar']
    };

    this.snackBar.open(`+${amount} XP kazandınız! ⭐`, '', config);
  }

  // Level up modal
  showLevelUp(levelUpResult: LevelUpResult): void {
    this.dialog.open(LevelUpModalComponent, {
      width: '600px',
      maxWidth: '90vw',
      disableClose: true,
      panelClass: 'level-up-dialog',
      data: levelUpResult
    });
  }

  // Streak milestone
  showStreakMilestone(days: number): void {
    const config: MatSnackBarConfig = {
      duration: 4000,
      horizontalPosition: 'center',
      verticalPosition: 'top',
      panelClass: ['streak-snackbar', 'success']
    };

    const icon = this.getStreakIcon(days);
    this.snackBar.open(
      `${icon} ${days} günlük seri! Harikasınız!`,
      'Kapat',
      config
    );
  }

  // Streak broken warning
  showStreakBrokenWarning(): void {
    const config: MatSnackBarConfig = {
      duration: 5000,
      horizontalPosition: 'center',
      verticalPosition: 'top',
      panelClass: ['streak-snackbar', 'warning']
    };

    this.snackBar.open(
      '⚠️ Seriniz kırılmak üzere! Bugün bir aktivite tamamlayın.',
      'Tamam',
      config
    );
  }

  // Streak frozen
  showStreakFrozen(): void {
    const config: MatSnackBarConfig = {
      duration: 3000,
      horizontalPosition: 'center',
      verticalPosition: 'top',
      panelClass: ['streak-snackbar', 'info']
    };

    this.snackBar.open(
      '❄️ Seri donduruldu! 1 gün molaya hakkınız var.',
      'Tamam',
      config
    );
  }

  private getStreakIcon(streak: number): string {
    if (streak >= 100) return '🔥🔥🔥🔥🔥';
    if (streak >= 30) return '🔥🔥🔥🔥';
    if (streak >= 14) return '🔥🔥🔥';
    if (streak >= 7) return '🔥🔥';
    if (streak >= 3) return '🔥';
    return '⚪';
  }
}
