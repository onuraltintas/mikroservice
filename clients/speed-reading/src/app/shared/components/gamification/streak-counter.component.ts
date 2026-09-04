import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-streak-counter',
  standalone: true,
  imports: [CommonModule, MatTooltipModule, MatIconModule],
  templateUrl: './streak-counter.component.html',
  styleUrls: ['./streak-counter.component.scss']
})
export class StreakCounterComponent {
  @Input() currentStreak: number = 0;
  @Input() longestStreak: number = 0;
  @Input() streakFreezeCount: number = 0;

  get streakIcon(): string {
    return this.currentStreak > 0 ? 'local_fire_department' : 'radio_button_unchecked';
  }

  get tooltipText(): string {
    return `Güncel Seri: ${this.currentStreak} gün\nEn Uzun Seri: ${this.longestStreak} gün`;
  }
}
