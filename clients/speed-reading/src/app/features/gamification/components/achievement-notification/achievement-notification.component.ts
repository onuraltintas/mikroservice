import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_SNACK_BAR_DATA, MatSnackBarRef } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Achievement } from '../../../../core/models/gamification.model';

export interface NotificationData {
  type: 'achievement' | 'xp' | 'streak' | 'level';
  title: string;
  message: string;
  achievement?: Achievement;
  xpAmount?: number;
  icon?: string;
}

@Component({
  selector: 'app-achievement-notification',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  templateUrl: './achievement-notification.component.html',
  styleUrls: ['./achievement-notification.component.scss']
})
export class AchievementNotificationComponent {
  constructor(
    @Inject(MAT_SNACK_BAR_DATA) public data: NotificationData,
    public snackBarRef: MatSnackBarRef<AchievementNotificationComponent>
  ) {}

  close(): void {
    this.snackBarRef.dismiss();
  }

  getTierColor(tier: string): string {
    const tierColors: { [key: string]: string } = {
      'Bronze': '#CD7F32',
      'Silver': '#C0C0C0',
      'Gold': '#FFD700',
      'Diamond': '#B9F2FF',
      'Special': '#FF6B9D'
    };
    return tierColors[tier] || '#9E9E9E';
  }
}
