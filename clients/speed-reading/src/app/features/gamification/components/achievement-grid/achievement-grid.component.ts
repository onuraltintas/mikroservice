import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { GamificationService } from '../../../../core/services/gamification.service';
import { Achievement, UserAchievement, AchievementCategory, AchievementTier } from '../../../../core/models/gamification.model';
import { AchievementBadgeComponent } from '../achievement-badge/achievement-badge.component';
import { forkJoin } from 'rxjs';

interface AchievementWithStatus extends Achievement {
  unlocked: boolean;
  unlockedAt?: Date;
  showcased: boolean;
}

@Component({
  selector: 'app-achievement-grid',
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AchievementBadgeComponent
  ],
  templateUrl: './achievement-grid.component.html',
  styleUrls: ['./achievement-grid.component.scss']
})
export class AchievementGridComponent implements OnInit {
  loading = true;
  achievements: AchievementWithStatus[] = [];
  filteredAchievements: AchievementWithStatus[] = [];
  selectedCategory: string | null = null;
  selectedTier: string | null = null;

  categories = Object.values(AchievementCategory);
  tiers = Object.values(AchievementTier);

  stats = {
    total: 0,
    unlocked: 0,
    showcased: 0,
    percentage: 0
  };

  constructor(private gamificationService: GamificationService) {}

  ngOnInit(): void {
    this.loadAchievements();
  }

  loadAchievements(): void {
    this.loading = true;

    forkJoin({
      allAchievements: this.gamificationService.getAllAchievements(),
      userAchievements: this.gamificationService.getUserAchievements()
    }).subscribe({
      next: ({ allAchievements, userAchievements }) => {
        this.achievements = allAchievements.map(achievement => {
          const userAchievement = userAchievements.find(
            ua => ua.achievementId === achievement.id
          );

          return {
            ...achievement,
            unlocked: !!userAchievement,
            unlockedAt: userAchievement?.unlockedAt,
            showcased: userAchievement?.isShowcased || false
          };
        });

        this.calculateStats();
        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading achievements:', error);
        this.loading = false;
      }
    });
  }

  calculateStats(): void {
    this.stats.total = this.achievements.length;
    this.stats.unlocked = this.achievements.filter(a => a.unlocked).length;
    this.stats.showcased = this.achievements.filter(a => a.showcased).length;
    this.stats.percentage = this.stats.total > 0
      ? Math.round((this.stats.unlocked / this.stats.total) * 100)
      : 0;
  }

  applyFilters(): void {
    this.filteredAchievements = this.achievements.filter(achievement => {
      const categoryMatch = !this.selectedCategory || achievement.category === this.selectedCategory;
      const tierMatch = !this.selectedTier || achievement.tier === this.selectedTier;
      return categoryMatch && tierMatch;
    });

    // Sort: unlocked first, then by tier, then by sortOrder
    this.filteredAchievements.sort((a, b) => {
      if (a.unlocked !== b.unlocked) return a.unlocked ? -1 : 1;
      if (a.tier !== b.tier) return this.getTierOrder(a.tier) - this.getTierOrder(b.tier);
      return a.sortOrder - b.sortOrder;
    });
  }

  getTierOrder(tier: string): number {
    const order: { [key: string]: number } = {
      'Bronze': 1,
      'Silver': 2,
      'Gold': 3,
      'Diamond': 4,
      'Special': 5
    };
    return order[tier] || 0;
  }

  filterByCategory(category: string | null): void {
    this.selectedCategory = this.selectedCategory === category ? null : category;
    this.applyFilters();
  }

  filterByTier(tier: string | null): void {
    this.selectedTier = this.selectedTier === tier ? null : tier;
    this.applyFilters();
  }

  clearFilters(): void {
    this.selectedCategory = null;
    this.selectedTier = null;
    this.applyFilters();
  }

  getCategoryIcon(category: string): string {
    const icons: { [key: string]: string } = {
      'Reading': '📖',
      'RSVP': '⚡',
      'Exercise': '💪',
      'Streak': '🔥',
      'Progress': '📊',
      'Special': '⭐'
    };
    return icons[category] || '🏆';
  }

  getCategoryText(category: string): string {
    const texts: { [key: string]: string } = {
      'Reading': 'Okuma',
      'RSVP': 'RSVP',
      'Exercise': 'Egzersiz',
      'Streak': 'Seri',
      'Progress': 'İlerleme',
      'Special': 'Özel'
    };
    return texts[category] || category;
  }

  getTierText(tier: string): string {
    const texts: { [key: string]: string } = {
      'Bronze': 'Bronz',
      'Silver': 'Gümüş',
      'Gold': 'Altın',
      'Diamond': 'Elmas',
      'Special': 'Özel'
    };
    return texts[tier] || tier;
  }
}
