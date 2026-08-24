import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GamificationService } from '../../../../core/services/gamification.service';

interface Achievement {
  id: string;
  title: string;
  description: string;
  icon: string;
  timestamp: Date;
}

@Component({
  selector: 'app-recent-achievements-widget',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './recent-achievements-widget.component.html',
  styleUrls: ['./recent-achievements-widget.component.scss']
})
export class RecentAchievementsWidgetComponent implements OnInit {
  private gamificationService = inject(GamificationService);

  achievements = signal<Achievement[]>([]);

  ngOnInit(): void {
    this.loadAchievements();
  }

  loadAchievements(): void {
    this.gamificationService.getUserAchievements().subscribe({
      next: (list) => {

        if (list.length > 0) {
          const mappedAchievements: Achievement[] = list.map((ua: any) => ({
            id: ua.achievement.id,
            title: ua.achievement.name,
            description: ua.achievement.description,
            icon: ua.achievement.iconEmoji || '',
            timestamp: new Date(ua.unlockedAt)
          }));

          // Sort by timestamp descending (newest first) and take top 5
          mappedAchievements.sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());
          this.achievements.set(mappedAchievements.slice(0, 5));
        }
      },
      error: (err) => {
        console.error('Error loading achievements:', err);
      }
    });
  }

  formatTime(date: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffHours / 24);

    if (diffHours < 1) return 'Az önce';
    if (diffHours < 24) return `${diffHours} saat önce`;
    if (diffDays === 1) return 'Dün';
    if (diffDays < 7) return `${diffDays} gün önce`;
    return date.toLocaleDateString('tr-TR');
  }
}
