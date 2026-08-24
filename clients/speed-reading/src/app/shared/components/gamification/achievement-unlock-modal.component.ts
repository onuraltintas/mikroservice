import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { trigger, transition, style, animate } from '@angular/animations';
import { Achievement } from '../../../core/models/gamification.model';

export interface AchievementUnlockData {
  achievements: Achievement[];
}

@Component({
  selector: 'app-achievement-unlock-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  templateUrl: './achievement-unlock-modal.component.html',
  styleUrls: ['./achievement-unlock-modal.component.scss'],
  animations: [
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('400ms ease-in', style({ opacity: 1 }))
      ])
    ]),
    trigger('slideDown', [
      transition(':enter', [
        style({ transform: 'translateY(-30px)', opacity: 0 }),
        animate('500ms ease-out', style({ transform: 'translateY(0)', opacity: 1 }))
      ])
    ]),
    trigger('scaleIn', [
      transition(':enter', [
        style({ transform: 'scale(0.8)', opacity: 0 }),
        animate('400ms 200ms ease-out', style({ transform: 'scale(1)', opacity: 1 }))
      ])
    ])
  ]
})
export class AchievementUnlockModalComponent {
  constructor(
    public dialogRef: MatDialogRef<AchievementUnlockModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AchievementUnlockData
  ) { }

  getTierColor(tier: string): string {
    const colors: { [key: string]: string } = {
      'Bronze': '#CD7F32',
      'Silver': '#C0C0C0',
      'Gold': '#FFD700',
      'Diamond': '#B9F2FF',
      'Special': '#9C27B0'
    };
    return colors[tier] || '#FFF';
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

  close(): void {
    this.dialogRef.close();
  }
}
