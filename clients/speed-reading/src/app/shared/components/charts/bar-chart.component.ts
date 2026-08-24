import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartType, ChartOptions } from 'chart.js';
import { ChartConfigService } from '../../../core/services/chart-config.service';

export interface BarChartDataPoint {
  name: string;
  value?: number; // Optional for ChartData compatibility
  series?: any[]; // For compatibility with ChartData from report.model.ts
}

@Component({
  selector: 'app-bar-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container" [style.height.px]="height">
      <canvas
        baseChart
        [data]="barChartData"
        [options]="barChartOptions"
        [type]="barChartType">
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
export class BarChartComponent implements AfterViewInit, OnChanges {
  private chartConfig = inject(ChartConfigService);

  @Input() data: BarChartDataPoint[] = [];
  @Input() xAxisLabel = '';
  @Input() yAxisLabel = '';
  @Input() showLegend = false;
  @Input() horizontal = false;
  @Input() height = 300;

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  barChartType: 'bar' = 'bar';
  barChartData: ChartData<'bar'> = {
    labels: [],
    datasets: []
  };
  barChartOptions: ChartOptions<'bar'> = {};

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

    // Check if data is ChartData[] format (has 'series' property)
    // or simple BarChartDataPoint[] format (has 'value' property)
    const isChartDataFormat = this.data.length > 0 && 'series' in this.data[0];

    let labels: string[];
    let values: number[];

    if (isChartDataFormat) {
      // ChartData[] format: [{name, series: [{name, value}]}]
      // Flatten to get first value from each series
      labels = (this.data as any[]).map(d => d.name);
      values = (this.data as any[]).map(d => d.series?.[0]?.value || 0);
    } else {
      // Simple format: [{name, value}]
      labels = this.data.map(d => d.name);
      values = this.data.map(d => d.value || 0);
    }

    this.barChartData = {
      labels,
      datasets: [{
        label: this.yAxisLabel || 'Value',
        data: values,
        backgroundColor: this.chartConfig.chartColors[0] + '80',
        borderColor: this.chartConfig.chartColors[0],
        borderWidth: 2
      }]
    };

    this.barChartOptions = this.chartConfig.getBarChartOptions({
      indexAxis: this.horizontal ? 'y' : 'x',
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

    this.chart?.update();
  }
}
