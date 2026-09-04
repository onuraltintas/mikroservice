import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType, ChartOptions } from 'chart.js';
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
        [attr.aria-label]="chartLabel"
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
      datasets = [{
        label: this.yAxisLabel || 'Value',
        data: (this.data as any[]).map(d => d.value),
        borderColor: this.chartConfig.chartColors[0],
        backgroundColor: this.chartConfig.chartColors[0] + '20',
        fill: true,
        tension: 0.4,
        pointRadius: 4,
        pointHoverRadius: 6
      }];
    } else {
      // New format: LineChartSeries[] = [{name, series: [{name, value}]}]
      labels = this.data[0]?.series?.map(d => d.name) || [];
      datasets = this.data.map((series, index) => ({
        label: series.name,
        data: series.series?.map(d => d.value) || [],
        borderColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length],
        backgroundColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length] + '20',
        fill: true,
        tension: 0.4,
        pointRadius: 4,
        pointHoverRadius: 6
      }));
    }

    this.lineChartData = {
      labels,
      datasets
    };

    this.lineChartOptions = this.chartConfig.getLineChartOptions({
      plugins: {
        legend: {
          display: this.showLegend
        }
      },
      scales: {
        x: {
          title: {
            display: !!this.xAxisLabel,
            text: this.xAxisLabel,
            font: {
              family: 'Roboto, sans-serif',
              size: 12,
              weight: 'bold'
            }
          }
        },
        y: {
          title: {
            display: !!this.yAxisLabel,
            text: this.yAxisLabel,
            font: {
              family: 'Roboto, sans-serif',
              size: 12,
              weight: 'bold'
            }
          }
        }
      }
    });

    // Update chart if it exists
    this.chart?.update();
  }
}
