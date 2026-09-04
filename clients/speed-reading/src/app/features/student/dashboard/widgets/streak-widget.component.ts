import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CountUpDirective } from '../../../../shared/directives/count-up.directive';
import { GamificationService } from '../../../../core/services/gamification.service';
import { UserGameification } from '../../../../core/models/gamification.model';

@Component({
  selector: 'app-streak-widget',
  standalone: true,
  imports: [CommonModule, CountUpDirective],
  templateUrl: './streak-widget.component.html',
  styleUrls: ['./streak-widget.component.scss']
})
export class StreakWidgetComponent implements OnInit {
  private gamificationService = inject(GamificationService);

  currentStreak  = signal(0);
  longestStreak  = signal(0);
  streakFreezeCount = signal(0);

  ngOnInit(): void {
    this.loadStreakData();
  }

  loadStreakData(): void {
    this.gamificationService.getUserGameification().subscribe({
      next: (data: UserGameification) => {
        if (data) {
          this.currentStreak.set(data.currentStreak || 0);
          this.longestStreak.set(data.longestStreak || 0);
          this.streakFreezeCount.set(data.streakFreezeCount || 0);
        }
      },
      error: (err: any) => console.error('Error loading streak data:', err)
    });
  }

  getFireIcons(): number[] {
    return Array.from({ length: Math.min(this.currentStreak(), 5) }, (_, i) => i);
  }

  getRemainingFireIcons(): number[] {
    return Array.from({ length: 5 - Math.min(this.currentStreak(), 5) }, (_, i) => i);
  }

  /** Son 7 günlük aktivite grid'i — streak sayısından yaklaşık hesaplar */
  getLast7Days(): { label: string; active: boolean; isToday: boolean }[] {
    const today = new Date();
    const streak = this.currentStreak();
    return Array.from({ length: 7 }, (_, i) => {
      const date = new Date(today);
      date.setDate(today.getDate() - (6 - i));
      const daysAgo = 6 - i;
      return {
        label: new Intl.DateTimeFormat('tr-TR', { weekday: 'narrow' }).format(date),
        active: daysAgo < streak,
        isToday: daysAgo === 0
      };
    });
  }

  getMotivationMessage(): string {
    const s = this.currentStreak();
    if (s === 0)  return 'Bugün başla, alışkanlık kazan!';
    if (s < 3)    return 'Harika başlangıç! Devam et!';
    if (s < 7)    return `${s} günlük seri! Hız kazanıyorsun`;
    if (s < 14)   return 'Bir haftayı geçtin! Muhteşem';
    if (s < 30)   return `${s} gün! Alışkanlık oluştu`;
    return `${s} gün! İnanılmaz kararlılık!`;
  }
}
