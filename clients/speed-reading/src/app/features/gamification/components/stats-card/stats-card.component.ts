import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './stats-card.component.html',
  styleUrls: ['./stats-card.component.scss']
})
export class StatsCardComponent {
  @Input() title: string = '';
  @Input() value: string | number = '';
  @Input() subtitle: string = '';
  @Input() icon: string = 'bar_chart';
  @Input() color: string = '#667eea';
  @Input() trend?: 'up' | 'down' | 'neutral';
  @Input() trendValue?: string;

  get gradientStyle() {
    return {
      'background': `linear-gradient(135deg, ${this.color} 0%, ${this.getDarkerColor(this.color)} 100%)`
    };
  }

  getDarkerColor(color: string): string {
    // Simple color darkening (hex to darker hex)
    const hex = color.replace('#', '');
    const r = parseInt(hex.substr(0, 2), 16);
    const g = parseInt(hex.substr(2, 2), 16);
    const b = parseInt(hex.substr(4, 2), 16);

    const darkerR = Math.max(0, r - 40);
    const darkerG = Math.max(0, g - 40);
    const darkerB = Math.max(0, b - 40);

    return `#${darkerR.toString(16).padStart(2, '0')}${darkerG.toString(16).padStart(2, '0')}${darkerB.toString(16).padStart(2, '0')}`;
  }

  getTrendIcon(): string {
    switch (this.trend) {
      case 'up': return 'trending_up';
      case 'down': return 'trending_down';
      case 'neutral': return 'trending_flat';
      default: return '';
    }
  }
}
