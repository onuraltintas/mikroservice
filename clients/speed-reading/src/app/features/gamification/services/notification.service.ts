import { Injectable, inject } from '@angular/core';
import { Achievement, LevelUpResult } from '../../../core/models/gamification.model';
import { MatDialog } from '@angular/material/dialog';
import { LevelUpModalComponent } from '../components/level-up-modal/level-up-modal.component';
import { ToasterService } from '../../../core/services/toaster.service';

@Injectable({
  providedIn: 'root'
})
export class GamificationNotificationService {
  private dialog = inject(MatDialog);
  private toaster = inject(ToasterService);

  // Achievement unlocked notification
  showAchievementUnlocked(achievement: Achievement): void {
    this.toaster.success(achievement.name, {
      title: 'Yeni başarı kazanıldı',
      duration: 5000
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
    this.toaster.info(`+${amount} XP kazandınız.`, {
      title: 'Deneyim puanı',
      duration: 3000
    });
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
    this.toaster.success(`${days} günlük seri! Harikasınız.`, {
      title: 'Seri hedefi',
      duration: 4000
    });
  }

  // Streak broken warning
  showStreakBrokenWarning(): void {
    this.toaster.warning('Seriniz kırılmak üzere. Bugün bir aktivite tamamlayın.', {
      title: 'Serinizi koruyun',
      duration: 5000,
      actionLabel: 'Tamam'
    });
  }

  // Streak frozen
  showStreakFrozen(): void {
    this.toaster.info('Seri donduruldu. Bir gün mola hakkınız var.', {
      title: 'Seri koruması',
      duration: 3000,
      actionLabel: 'Tamam'
    });
  }
}
