import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatIconModule } from '@angular/material/icon';
import { Achievement, AchievementTier } from '../../../../core/models/gamification.model';

@Component({
  selector: 'app-achievement-badge',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatTooltipModule, MatIconModule],
  templateUrl: './achievement-badge.component.html',
  styleUrls: ['./achievement-badge.component.scss']
})
export class AchievementBadgeComponent {
  @Input() achievement!: Achievement;
  @Input() unlocked: boolean = false;
  @Input() unlockedAt?: Date;
  @Input() showcased: boolean = false;
  @Input() size: 'small' | 'medium' | 'large' = 'medium';

  get tierClass(): string {
    return `tier-${this.achievement.tier.toLowerCase()}`;
  }

  get tooltipText(): string {
    let text = `${this.achievement.name}\n${this.achievement.description}`;
    if (this.unlocked && this.unlockedAt) {
      text += `\n\nKazanıldı: ${new Date(this.unlockedAt).toLocaleDateString('tr-TR')}`;
    }
    text += `\n+${this.achievement.xpReward} XP`;
    return text;
  }

  getTierColor(tier: string): string {
    const tierColors: { [key: string]: string } = {
      'Bronze': '#CD7F32',
      'Silver': '#C0C0C0',
      'Gold': '#FFD700',
      'Diamond': '#B9F2FF',
      'Special': '#FF6B9D'
    };
    return tierColors[tier] || '#9E9E9E';
  }

  getTierText(tier: string): string {
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
