import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { GamificationService } from '../../../core/services/gamification.service';
import { Achievement, UserAchievement, AchievementCategory } from '../../../core/models/gamification.model';
import { BadgeCardComponent } from '../../../shared/components/gamification/badge-card.component';

interface AchievementWithStatus extends Achievement {
  userAchievement?: UserAchievement;
}

@Component({
  selector: 'app-achievements',
  standalone: true,
  imports: [CommonModule, BadgeCardComponent],
  templateUrl: './achievements.component.html',
  styleUrl: './achievements.component.scss'
})
export class AchievementsComponent implements OnInit, OnDestroy {
  private readonly gamificationService = inject(GamificationService);
  private destroy$ = new Subject<void>();

  achievements: AchievementWithStatus[] = [];
  loading = true;
  selectedCategory: string = 'All';

  categories = [
    { value: 'All', label: 'Tümü', icon: 'workspace_premium' },
    { value: 'Reading', label: 'Okuma', icon: 'book' },
    { value: 'RSVP', label: 'RSVP', icon: 'speed' },
    { value: 'Exercise', label: 'Egzersiz', icon: 'fitness_center' },
    { value: 'Streak', label: 'Seri', icon: 'local_fire_department' },
    { value: 'Progress', label: 'İlerleme', icon: 'trending_up' },
    { value: 'Special', label: 'Özel', icon: 'stars' }
  ];

  ngOnInit(): void {
    this.loadAchievements();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAchievements(): void {
    this.loading = true;
    forkJoin({
      allAchievements: this.gamificationService.getAllAchievements(),
      userAchievements: this.gamificationService.getUserAchievements()
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ allAchievements, userAchievements }) => {
          const achList = Array.isArray(allAchievements) ? allAchievements : [];
          const uaList = Array.isArray(userAchievements) ? userAchievements : [];
          // Map user achievements to all achievements
          this.achievements = achList.map(achievement => {
            const userAchievement = uaList.find(
              ua => ua.achievementId === achievement.id
            );
            return {
              ...achievement,
              userAchievement
            };
          }).sort((a, b) => {
            // Define tier order
            const tierOrder: { [key: string]: number } = {
              'Bronze': 1,
              'Silver': 2,
              'Gold': 3,
              'Diamond': 4,
              'Special': 5
            };

            // Sort by tier first
            const aTierOrder = tierOrder[a.tier] || 999;
            const bTierOrder = tierOrder[b.tier] || 999;

            if (aTierOrder !== bTierOrder) {
              return aTierOrder - bTierOrder;
            }

            // Within same tier, sort by earned status (earned first)
            const aEarned = !!a.userAchievement;
            const bEarned = !!b.userAchievement;

            if (aEarned && !bEarned) return -1;
            if (!aEarned && bEarned) return 1;
            return 0;
          });
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading achievements:', err);
          this.loading = false;
        }
      });
  }

  get filteredAchievements(): AchievementWithStatus[] {
    if (this.selectedCategory === 'All') {
      return this.achievements;
    }
    return this.achievements.filter(a => a.category === this.selectedCategory);
  }

  get unlockedCount(): number {
    return this.achievements.filter(a => a.userAchievement).length;
  }

  get totalCount(): number {
    return this.achievements.length;
  }

  get progressPercentage(): number {
    if (this.totalCount === 0) return 0;
    return Math.round((this.unlockedCount / this.totalCount) * 100);
  }

  get totalXPEarned(): number {
    return this.achievements
      .filter(a => a.userAchievement)
      .reduce((sum, a) => sum + a.xpReward, 0);
  }

  getCategoryStats(category: string): { unlocked: number; total: number } {
    const categoryAchievements = this.achievements.filter(
      a => a.category === category
    );
    return {
      unlocked: categoryAchievements.filter(a => a.userAchievement).length,
      total: categoryAchievements.length
    };
  }

  selectCategory(category: string): void {
    this.selectedCategory = category;
  }

  getAchievementsByTier(tier: string): AchievementWithStatus[] {
    return this.filteredAchievements.filter(a => a.tier === tier);
  }

  getTierLabel(tier: string): string {
    const tierMap: { [key: string]: string } = {
      'Bronze': 'Bronz',
      'Silver': 'Gümüş',
      'Gold': 'Altın',
      'Diamond': 'Elmas',
      'Special': 'Özel'
    };
    return tierMap[tier] || tier;
  }
}
