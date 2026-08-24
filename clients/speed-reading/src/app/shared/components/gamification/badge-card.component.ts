import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Achievement, UserAchievement } from '../../../core/models/gamification.model';

@Component({
  selector: 'app-badge-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './badge-card.component.html',
  styleUrls: ['./badge-card.component.scss']
})
export class BadgeCardComponent {
  @Input() achievement!: Achievement;
  @Input() userAchievement?: UserAchievement;

  get isUnlocked(): boolean {
    return !!this.userAchievement;
  }

  get iconFilter(): string {
    return this.isUnlocked ? 'none' : 'grayscale(100%) opacity(0.4)';
  }

  get tierColor(): string {
    const colors: { [key: string]: string } = {
      'Bronze':  'var(--sp-streak)',   // turuncu
      'Silver':  'var(--sp-text-2)',   // gri/gümüş
      'Gold':    'var(--sp-xp)',       // altın sarısı
      'Diamond': 'var(--sp-diamond)',  // camgöbeği
      'Special': 'var(--sp-primary)'  // indigo
    };
    return colors[this.achievement.tier] || 'var(--sp-text-3)';
  }

  get tierText(): string {
    const tierMap: { [key: string]: string } = {
      'Bronze': 'Bronz',
      'Silver': 'Gümüş',
      'Gold': 'Altın',
      'Diamond': 'Elmas',
      'Special': 'Özel'
    };
    return tierMap[this.achievement.tier] || this.achievement.tier;
  }

  get tooltipText(): string {
    return this.achievement.description;
  }
}
