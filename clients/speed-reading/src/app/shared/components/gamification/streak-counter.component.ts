import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-streak-counter',
  standalone: true,
  imports: [CommonModule, MatTooltipModule],
  templateUrl: './streak-counter.component.html',
  styleUrls: ['./streak-counter.component.scss']
})
export class StreakCounterComponent {
  @Input() currentStreak: number = 0;
  @Input() longestStreak: number = 0;
  @Input() streakFreezeCount: number = 0;

  get streakIcon(): string {
    if (this.currentStreak >= 100) return '🔥🔥🔥🔥🔥';
    if (this.currentStreak >= 30) return '🔥🔥🔥🔥';
    if (this.currentStreak >= 14) return '🔥🔥🔥';
    if (this.currentStreak >= 7) return '🔥🔥';
    if (this.currentStreak >= 3) return '🔥';
    return '⚪';
  }

  get tooltipText(): string {
    return `Güncel Seri: ${this.currentStreak} gün\nEn Uzun Seri: ${this.longestStreak} gün`;
  }
}
