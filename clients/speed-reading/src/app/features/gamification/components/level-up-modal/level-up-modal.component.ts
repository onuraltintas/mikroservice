import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LevelUpResult } from '../../../../core/models/gamification.model';
import { AchievementBadgeComponent } from '../achievement-badge/achievement-badge.component';

@Component({
  selector: 'app-level-up-modal',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    AchievementBadgeComponent
  ],
  templateUrl: './level-up-modal.component.html',
  styleUrls: ['./level-up-modal.component.scss']
})
export class LevelUpModalComponent {
  constructor(
    public dialogRef: MatDialogRef<LevelUpModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: LevelUpResult
  ) {}

  getTierIcon(level: number): string {
    const tier = Math.ceil(level / 5);
    const icons = ['🌟', '⭐', '✨', '💎'];
    return icons[tier - 1] || '🌟';
  }

  getTierColor(level: number): string {
    const tier = Math.ceil(level / 5);
    const colors = ['#CD7F32', '#C0C0C0', '#FFD700', '#B9F2FF'];
    return colors[tier - 1] || '#CD7F32';
  }

  getTierName(level: number): string {
    const tier = Math.ceil(level / 5);
    const names = ['Bronz', 'Gümüş', 'Altın', 'Elmas'];
    return names[tier - 1] || 'Bronz';
  }

  formatXP(xp: number): string {
    if (xp >= 1000000) return `${(xp / 1000000).toFixed(1)}M`;
    if (xp >= 1000) return `${(xp / 1000).toFixed(1)}K`;
    return xp.toString();
  }

  close(): void {
    this.dialogRef.close();
  }
}
