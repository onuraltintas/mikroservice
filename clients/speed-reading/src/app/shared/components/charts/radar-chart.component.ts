import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartOptions } from 'chart.js';
import { ChartConfigService } from '../../../core/services/chart-config.service';

export interface RadarChartDataPoint {
  name: string;
  value: number;
}

export interface RadarChartSeries {
  name: string;
  series?: RadarChartDataPoint[]; // Optional for compatibility with ChartSeries
  value?: number; // For compatibility with ChartSeries
}

@Component({
  selector: 'app-radar-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container">
      <canvas
        baseChart
        role="img"
        [attr.aria-label]="chartLabel"
        [data]="radarChartData"
        [options]="radarChartOptions"
        [type]="'radar'">
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
export class RadarChartComponent implements AfterViewInit, OnChanges {
  private chartConfig = inject(ChartConfigService);

  @Input() data: RadarChartSeries[] = [];
  @Input() showLegend = true;
  @Input() height = 300;
  @Input() chartLabel = 'Radar grafiği';

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  radarChartData: ChartData<'radar'> = {
    labels: [],
    datasets: []
  };
  radarChartOptions: ChartOptions<'radar'> = {};

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

    // Check if data is in ChartSeries[] format (simple {name, value})
    // or RadarChartSeries[] format ({name, series: [{name, value}]})
    const isSimpleFormat = this.data.length > 0 && 'value' in this.data[0] && !('series' in this.data[0]);

    let labels: string[];
    let datasets: any[];

    if (isSimpleFormat) {
      // Simple format: just plot the values as a single series
      labels = (this.data as any[]).map(d => d.name);
      datasets = [{
        label: 'Values',
        data: (this.data as any[]).map(d => d.value),
        backgroundColor: this.chartConfig.chartColors[0] + '40',
        borderColor: this.chartConfig.chartColors[0],
        borderWidth: 2,
        pointBackgroundColor: this.chartConfig.chartColors[0],
        pointBorderColor: '#fff',
        pointHoverBackgroundColor: '#fff',
        pointHoverBorderColor: this.chartConfig.chartColors[0]
      }];
    } else {
      // Complex format with series
      labels = this.data[0]?.series?.map(d => d.name) || [];
      datasets = this.data.map((series, index) => ({
        label: series.name,
        data: series.series?.map(d => d.value) || [],
        backgroundColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length] + '40',
        borderColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length],
        borderWidth: 2,
        pointBackgroundColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length],
        pointBorderColor: '#fff',
        pointHoverBackgroundColor: '#fff',
        pointHoverBorderColor: this.chartConfig.chartColors[index % this.chartConfig.chartColors.length]
      }));
    }

    this.radarChartData = {
      labels,
      datasets
    };

    this.radarChartOptions = this.chartConfig.getRadarChartOptions({
      plugins: {
        legend: {
          display: this.showLegend
        }
      }
    });

    this.chart?.update();
  }
}
