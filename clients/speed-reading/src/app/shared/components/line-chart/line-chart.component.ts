import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ChartDataPoint {
  label: string;
  value: number;
}

@Component({
  selector: 'app-line-chart',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './line-chart.component.html',
  styleUrls: ['./line-chart.component.scss']
})
export class LineChartComponent implements OnChanges {
  @Input() data: ChartDataPoint[] = [];
  @Input() title: string = '';
  @Input() color: string = '#2196f3';
  @Input() height: number = 200;
  @Input() showGrid: boolean = true;
  @Input() showPoints: boolean = true;

  points: string = '';
  maxValue: number = 0;
  minValue: number = 0;
  chartWidth: number = 600;
  chartHeight: number = 200;
  padding = { top: 20, right: 20, bottom: 40, left: 50 };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] || changes['height']) {
      this.chartHeight = this.height;
      this.calculateChart();
    }
  }

  calculateChart(): void {
    if (!this.data || this.data.length === 0) {
      this.points = '';
      return;
    }

    const values = this.data.map(d => d.value);
    this.maxValue = Math.max(...values);
    this.minValue = Math.min(...values);

    // Add some padding to max/min values
    const range = this.maxValue - this.minValue;
    this.maxValue += range * 0.1;
    this.minValue = Math.max(0, this.minValue - range * 0.1);

    const plotWidth = this.chartWidth - this.padding.left - this.padding.right;
    const plotHeight = this.chartHeight - this.padding.top - this.padding.bottom;

    const points = this.data.map((point, index) => {
      const x = this.padding.left + (index / (this.data.length - 1)) * plotWidth;
      const y = this.padding.top + plotHeight - ((point.value - this.minValue) / (this.maxValue - this.minValue)) * plotHeight;
      return `${x},${y}`;
    });

    this.points = points.join(' ');
  }

  getGridLines(): number[] {
    return [0, 0.25, 0.5, 0.75, 1];
  }

  getYPosition(ratio: number): number {
    const plotHeight = this.chartHeight - this.padding.top - this.padding.bottom;
    return this.padding.top + plotHeight * (1 - ratio);
  }

  getYValue(ratio: number): number {
    return Math.round(this.minValue + (this.maxValue - this.minValue) * ratio);
  }

  getPointsArray(): Array<{x: number, y: number, label: string, value: number}> {
    if (!this.data || this.data.length === 0) return [];

    const plotWidth = this.chartWidth - this.padding.left - this.padding.right;
    const plotHeight = this.chartHeight - this.padding.top - this.padding.bottom;

    return this.data.map((point, index) => ({
      x: this.padding.left + (index / (this.data.length - 1)) * plotWidth,
      y: this.padding.top + plotHeight - ((point.value - this.minValue) / (this.maxValue - this.minValue)) * plotHeight,
      label: point.label,
      value: point.value
    }));
  }

  getXLabelPosition(index: number): number {
    const plotWidth = this.chartWidth - this.padding.left - this.padding.right;
    return this.padding.left + (index / (this.data.length - 1)) * plotWidth;
  }
}
