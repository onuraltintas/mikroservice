import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { BloomPerformance, BloomLevel, BLOOM_LEVEL_LABELS, BLOOM_LEVEL_COLORS } from '../../../core/models/reading-text.model';

@Component({
  selector: 'app-bloom-performance-chart',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './bloom-performance-chart.component.html',
  styleUrl: './bloom-performance-chart.component.scss'
})
export class BloomPerformanceChartComponent {
  @Input() performanceData: BloomPerformance[] = [];
  @Input() showTitle = true;
  @Input() compact = false;

  readonly bloomLabels = BLOOM_LEVEL_LABELS;
  readonly bloomColors = BLOOM_LEVEL_COLORS;

  getStrongestLevel(): BloomPerformance | undefined {
    if (!this.performanceData.length) return undefined;
    return this.performanceData.reduce((max, item) =>
      item.accuracyPercentage > max.accuracyPercentage ? item : max
    );
  }

  getWeakestLevel(): BloomPerformance | undefined {
    if (!this.performanceData.length) return undefined;
    return this.performanceData.reduce((min, item) =>
      item.accuracyPercentage < min.accuracyPercentage ? item : min
    );
  }

  getPerformanceColor(category: string): string {
    switch (category) {
      case 'Mükemmel': return '#4CAF50';
      case 'İyi': return '#2196F3';
      case 'Orta': return '#FF9800';
      case 'Geliştirilmeli': return '#F44336';
      default: return '#9E9E9E';
    }
  }

  getPerformanceIcon(category: string): string {
    switch (category) {
      case 'Mükemmel': return 'stars';
      case 'İyi': return 'thumb_up';
      case 'Orta': return 'trending_up';
      case 'Geliştirilmeli': return 'trending_down';
      default: return 'help';
    }
  }

  getDifficultyStars(level: number): string {
    return '★'.repeat(level) + '☆'.repeat(5 - level);
  }
}
