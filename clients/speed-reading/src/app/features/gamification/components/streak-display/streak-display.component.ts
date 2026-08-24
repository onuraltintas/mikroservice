import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';

interface StreakDay {
  date: Date;
  hasActivity: boolean;
  isToday: boolean;
  isFuture: boolean;
  dayOfWeek: string;
}

@Component({
  selector: 'app-streak-display',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatButtonModule, MatTooltipModule],
  templateUrl: './streak-display.component.html',
  styleUrls: ['./streak-display.component.scss']
})
export class StreakDisplayComponent implements OnInit {
  @Input() currentStreak: number = 0;
  @Input() longestStreak: number = 0;
  @Input() lastActivityDate?: Date;
  @Input() streakFreezeCount: number = 0;

  streakDays: StreakDay[] = [];
  streakStatus: 'active' | 'broken' | 'frozen' = 'active';

  ngOnInit(): void {
    this.generateStreakCalendar();
    this.checkStreakStatus();
  }

  ngOnChanges(): void {
    this.generateStreakCalendar();
    this.checkStreakStatus();
  }

  generateStreakCalendar(): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    this.streakDays = [];

    // Show last 7 days
    for (let i = 6; i >= 0; i--) {
      const date = new Date(today);
      date.setDate(date.getDate() - i);

      const isToday = i === 0;
      const hasActivity = this.hasActivityOnDate(date);

      this.streakDays.push({
        date,
        hasActivity,
        isToday,
        isFuture: false,
        dayOfWeek: this.getDayOfWeek(date)
      });
    }
  }

  hasActivityOnDate(date: Date): boolean {
    if (!this.lastActivityDate) return false;

    const lastActivity = new Date(this.lastActivityDate);
    lastActivity.setHours(0, 0, 0, 0);

    // Check if date is within current streak
    const daysDiff = Math.floor((new Date().getTime() - date.getTime()) / (1000 * 60 * 60 * 24));
    return daysDiff < this.currentStreak;
  }

  checkStreakStatus(): void {
    if (!this.lastActivityDate) {
      this.streakStatus = 'broken';
      return;
    }

    const lastActivity = new Date(this.lastActivityDate);
    const today = new Date();
    const daysDiff = Math.floor((today.getTime() - lastActivity.getTime()) / (1000 * 60 * 60 * 24));

    if (daysDiff === 0) {
      this.streakStatus = 'active';
    } else if (daysDiff === 1 && this.streakFreezeCount > 0) {
      this.streakStatus = 'frozen';
    } else if (daysDiff > 1) {
      this.streakStatus = 'broken';
    } else {
      this.streakStatus = 'active';
    }
  }

  getDayOfWeek(date: Date): string {
    const days = ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'];
    return days[date.getDay()];
  }

  getStreakIcon(streak: number): string {
    if (streak >= 100) return '🔥🔥🔥🔥🔥';
    if (streak >= 30) return '🔥🔥🔥🔥';
    if (streak >= 14) return '🔥🔥🔥';
    if (streak >= 7) return '🔥🔥';
    if (streak >= 3) return '🔥';
    return '⚪';
  }

  getStreakMessage(): string {
    if (this.currentStreak === 0) {
      return 'Bir seri başlatmak için bugün aktivite yapın!';
    }
    if (this.currentStreak < 7) {
      return `${7 - this.currentStreak} gün daha devam edin!`;
    }
    if (this.currentStreak < 30) {
      return 'Harika gidiyorsunuz! Devam edin! 🔥';
    }
    return 'Muhteşem bir seri! Efsanesiniz! 🔥🔥🔥';
  }

  getMotivationMessage(): string {
    const messages = [
      'Her gün biraz daha iyileşiyorsunuz! 💪',
      'Düzenli çalışmanın gücünü görüyorsunuz! ⭐',
      'Harika bir tempo! Devam edin! 🚀',
      'Azminiz takdire şayan! 🏆'
    ];
    return messages[Math.floor(Math.random() * messages.length)];
  }
}
