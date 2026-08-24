import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

interface Badge {
  id: string;
  name: string;
  icon: string;
  description: string;
  unlocked: boolean;
  progress?: number;
}
import { GamificationService } from '../../../../core/services/gamification.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-level-badges-widget',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule
  ],
  templateUrl: './level-badges-widget.component.html',
  styleUrls: ['./level-badges-widget.component.scss']
})
export class LevelBadgesWidgetComponent implements OnInit {
  private gamificationService = inject(GamificationService);

  currentLevel = signal(1);
  currentXP = signal(0);
  maxXP = signal(100);
  badges = signal<Badge[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    forkJoin({
      userGamification: this.gamificationService.getUserGameification(),
      allAchievements: this.gamificationService.getAllAchievements(),
      userAchievements: this.gamificationService.getUserAchievements()
    }).subscribe({
      next: (result: any) => {
        const { userGamification, allAchievements, userAchievements } = result;

        // Level & XP
        if (userGamification) {
          this.currentLevel.set(userGamification.currentLevel);
          this.currentXP.set(userGamification.currentLevelXP);
          this.maxXP.set(userGamification.nextLevelXP);
        }

        // Badges
        const achArray: any[] = Array.isArray(allAchievements) ? allAchievements : [];
        const uaArray: any[] = Array.isArray(userAchievements) ? userAchievements : [];
        const mappedBadges: Badge[] = achArray.map((ach: any) => {
          const isUnlocked = uaArray.some((ua: any) => ua.achievementId === ach.id);
          return {
            id: ach.id,
            name: ach.name,
            icon: ach.iconEmoji || 'emoji_events',
            description: ach.description,
            unlocked: isUnlocked,
            progress: isUnlocked ? 100 : 0
          };
        });

        // Sort: Unlocked first, then by sort order
        mappedBadges.sort((a, b) => {
          if (a.unlocked && !b.unlocked) return -1;
          if (!a.unlocked && b.unlocked) return 1;
          return 0;
        });

        // Take top 4 for display
        this.badges.set(mappedBadges.slice(0, 4));
      },
      error: (err) => {
        console.error('Error loading gamification data:', err);
      }
    });
  }

  getXPPercentage(): number {
    return (this.currentXP() / this.maxXP()) * 100;
  }
}
