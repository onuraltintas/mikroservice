import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { trigger, transition, style, animate, query, stagger } from '@angular/animations';
import { LevelUpResult } from '../../../core/models/gamification.model';

@Component({
  selector: 'app-level-up-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './level-up-modal.component.html',
  styleUrls: ['./level-up-modal.component.scss'],
  animations: [
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('500ms ease-in', style({ opacity: 1 }))
      ])
    ]),
    trigger('slideDown', [
      transition(':enter', [
        style({ transform: 'translateY(-50px)', opacity: 0 }),
        animate('600ms ease-out', style({ transform: 'translateY(0)', opacity: 1 }))
      ])
    ]),
    trigger('scaleIn', [
      transition(':enter', [
        style({ transform: 'scale(0.5)', opacity: 0 }),
        animate('500ms 200ms ease-out', style({ transform: 'scale(1)', opacity: 1 }))
      ])
    ]),
    trigger('staggerIn', [
      transition(':enter', [
        query('.stat-item', [
          style({ transform: 'translateY(20px)', opacity: 0 }),
          stagger(100, [
            animate('400ms ease-out', style({ transform: 'translateY(0)', opacity: 1 }))
          ])
        ], { optional: true })
      ])
    ])
  ]
})
export class LevelUpModalComponent {
  constructor(
    public dialogRef: MatDialogRef<LevelUpModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: LevelUpResult
  ) { }

  get oldTierIcon(): string {
    const tier = Math.ceil(this.data.oldLevel / 5);
    switch (tier) {
      case 1: return 'menu_book';
      case 2: return 'auto_stories';
      case 3: return 'library_books';
      case 4: return 'workspace_premium';
      default: return 'menu_book';
    }
  }

  normalizeIcon(icon: string | undefined): string {
    return icon && /^[a-z0-9_]+$/i.test(icon) ? icon : 'auto_awesome';
  }

  get tierGradient(): string {
    const tier = Math.ceil(this.data.newLevel / 5);
    switch (tier) {
      case 1: return 'linear-gradient(135deg, #9E9E9E 0%, #BDBDBD 100%)';
      case 2: return 'linear-gradient(135deg, #4CAF50 0%, #66BB6A 100%)';
      case 3: return 'linear-gradient(135deg, #2196F3 0%, #42A5F5 100%)';
      case 4: return 'linear-gradient(135deg, #FFD700 0%, #FFC107 100%)';
      default: return 'linear-gradient(135deg, #9E9E9E 0%, #BDBDBD 100%)';
    }
  }

  close(): void {
    this.dialogRef.close();
  }
}
