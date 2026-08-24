import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface HeatmapData {
  date: string;
  value: number;
  level: 0 | 1 | 2 | 3 | 4;
}

@Component({
  selector: 'app-heatmap',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="heatmap-container">
      <div class="heatmap-grid">
        <div 
          *ngFor="let cell of data" 
          class="heatmap-cell"
          [class]="'level-' + cell.level"
          [title]="cell.date + ': ' + cell.value">
        </div>
      </div>
      <div class="heatmap-legend">
        <span>Less</span>
        <div class="legend-cell level-0"></div>
        <div class="legend-cell level-1"></div>
        <div class="legend-cell level-2"></div>
        <div class="legend-cell level-3"></div>
        <div class="legend-cell level-4"></div>
        <span>More</span>
      </div>
    </div>
  `,
  styles: [`
    .heatmap-container {
      padding: 16px;
    }
    .heatmap-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12px, 1fr));
      gap: 3px;
      margin-bottom: 16px;
    }
    .heatmap-cell {
      width: 12px;
      height: 12px;
      border-radius: 2px;
      cursor: pointer;
      transition: transform 0.2s;
    }
    .heatmap-cell:hover {
      transform: scale(1.2);
    }
    .level-0 { background-color: #e0e7ff; }
    .level-1 { background-color: #a5b4fc; }
    .level-2 { background-color: #818cf8; }
    .level-3 { background-color: #6366f1; }
    .level-4 { background-color: #4f46e5; }
    .heatmap-legend {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
      color: var(--sp-text-3, #6b7280);
    }
    .legend-cell {
      width: 12px;
      height: 12px;
      border-radius: 2px;
    }
  `]
})
export class HeatmapComponent {
  @Input() data: HeatmapData[] = [];
}
