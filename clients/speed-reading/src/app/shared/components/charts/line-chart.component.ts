import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartOptions } from 'chart.js';
import { ChartConfigService } from '../../../core/services/chart-config.service';

export interface LineChartDataPoint {
  name: string;
  value: number;
}

export interface LineChartSeries {
  name: string;
  series?: LineChartDataPoint[]; // Optional for ChartSeries compatibility
  value?: number; // For compatibility with ChartSeries from report.model.ts
}

@Component({
  selector: 'app-line-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container" [style.height.px]="height">
      <canvas
        baseChart
        role="img"
        [attr.aria-label]="chartLabel + (valueLabel ? ': ' + valueLabel : '')"
        [data]="lineChartData"
        [options]="lineChartOptions"
        [type]="lineChartType">
      </canvas>
    </div>
  `,
  styles: [`
    .chart-container {
      position: relative;
      height: 300px;
      width: 100%;
    }
  `]
})
export class LineChartComponent implements AfterViewInit, OnChanges {
  private chartConfig = inject(ChartConfigService);

  @Input() data: LineChartSeries[] = [];
  @Input() xAxisLabel = '';
  @Input() yAxisLabel = '';
  @Input() showLegend = true;
  @Input() height = 300;
  @Input() chartLabel = 'Çizgi grafiği';
  /** Value axis starts at zero unless a trend reads better zoomed (e.g. reading speed). */
  @Input() beginAtZero = true;
  @Input() suggestedMax?: number;
  /** Values for screen readers (design rule: chart values are in the aria-label). */
  valueLabel = '';

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  lineChartType: 'line' = 'line';
  lineChartData: ChartData<'line'> = {
    labels: [],
    datasets: []
  };
  lineChartOptions: ChartOptions<'line'> = {};

  ngAfterViewInit(): void {
    this.updateChart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.updateChart();
    }
  }

  private updateChart(): void {
    if (!this.data || this.data.length === 0) {
      return;
    }

    // Check if data is in old ChartSeries[] format (array of {name, value})
    // or new LineChartSeries[] format (array of {name, series: [{name, value}]})
    const isOldFormat = this.data.length > 0 && 'value' in this.data[0] && !('series' in this.data[0]);

    let labels: string[];
    let datasets: any[];

    if (isOldFormat) {
      // Old format: ChartSeries[] = [{name, value}, {name, value}, ...]
      labels = (this.data as any[]).map(d => d.name);
      datasets = [this.styleSeries(this.yAxisLabel || 'Değer', (this.data as any[]).map(d => d.value), 0)];
    } else {
      // New format: LineChartSeries[] = [{name, series: [{name, value}]}]
      labels = this.data[0]?.series?.map(d => d.name) || [];
      datasets = this.data.map((series, index) => this.styleSeries(series.name, series.series?.map(d => d.value) || [], index));
    }

    this.lineChartData = {
      labels,
      datasets
    };

    const axes = this.chartConfig.axes(this.beginAtZero) as any;
    const title = (text: string) => ({ display: !!text, text, color: this.chartConfig.colors.muted,
      font: { family: this.chartConfig.fontFamily, size: 12, weight: 'bold' as const } });
    this.lineChartOptions = this.chartConfig.getLineChartOptions({
      maintainAspectRatio: false,
      plugins: { legend: { display: this.showLegend && datasets.length > 1 } },
      scales: {
        x: { ...axes.x, title: title(this.xAxisLabel), ticks: { ...axes.x.ticks, maxTicksLimit: 8, maxRotation: 0 } },
        y: { ...axes.y, title: title(this.yAxisLabel), suggestedMax: this.suggestedMax }
      }
    });
    this.valueLabel = this.describe(labels, datasets);

    // Update chart if it exists
    this.chart?.update();
  }

  /** Brand line: pine (or the next brand colour), 12 % fill, small surface-filled points. */
  private styleSeries(label: string, data: number[], index: number) {
    const palette = this.chartConfig.chartColors;
    const color = palette[index % palette.length];
    return {
      label, data,
      borderColor: color,
      backgroundColor: this.chartConfig.withAlpha(color, 0.12),
      fill: index === 0,
      tension: 0.3,
      borderWidth: 2.5,
      pointRadius: data.length > 40 ? 0 : 3,
      pointHoverRadius: 6,
      pointBackgroundColor: this.chartConfig.colors.surface,
      pointBorderColor: color,
      pointBorderWidth: 2
    };
  }

  private describe(labels: string[], datasets: { label: string; data: number[] }[]): string {
    return datasets.map(set => {
      const pairs = set.data.map((value, i) => `${labels[i] ?? ''} ${Math.round(value)}`);
      const shown = pairs.length > 12 ? [...pairs.slice(0, 2), '…', ...pairs.slice(-6)] : pairs;
      return `${set.label}: ${shown.join(', ')}`;
    }).join('; ');
  }
}
