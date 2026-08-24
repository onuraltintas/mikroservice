import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { trigger, state, style, transition, animate } from '@angular/animations';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  imports: [CommonModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: './progress-bar.component.html',
  styleUrls: ['./progress-bar.component.scss'],
  animations: [
    trigger('progressAnimation', [
      state('*', style({ width: '{{width}}%' }), { params: { width: 0 } }),
      transition('* => *', animate('800ms cubic-bezier(0.4, 0.0, 0.2, 1)'))
    ])
  ]
})
export class ProgressBarComponent implements OnInit {
  @Input() currentXP: number = 0;
  @Input() nextLevelXP: number = 100;
  @Input() level: number = 1;
  @Input() levelTitle: string = 'Başlangıç Okuyucu';
  @Input() levelIcon: string = '📖';
  @Input() totalXP: number = 0;
  @Input() showDetails: boolean = true;

  progressPercentage: number = 0;
  animatedPercentage: number = 0;

  ngOnInit(): void {
    this.calculateProgress();
  }

  ngOnChanges(): void {
    this.calculateProgress();
  }

  calculateProgress(): void {
    if (this.nextLevelXP > 0) {
      this.progressPercentage = Math.min((this.currentXP / this.nextLevelXP) * 100, 100);

      // Animate progress
      setTimeout(() => {
        this.animatedPercentage = this.progressPercentage;
      }, 100);
    }
  }

  get progressColor(): string {
    return this.getTierColor(this.level);
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
      case 1: return 'Başlangıç';
      case 2: return 'Gelişen';
      case 3: return 'İleri';
      case 4: return 'Master';
      default: return 'Okuyucu';
    }
  }

  formatXP(xp: number): string {
    if (xp >= 1000000) {
      return (xp / 1000000).toFixed(1) + 'M';
    }
    if (xp >= 1000) {
      return (xp / 1000).toFixed(1) + 'K';
    }
    return xp.toString();
  }
}
