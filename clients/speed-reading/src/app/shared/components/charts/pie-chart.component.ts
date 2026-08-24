import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartType, ChartOptions } from 'chart.js';
import { ChartConfigService } from '../../../core/services/chart-config.service';

export interface PieChartDataPoint {
  name: string;
  value?: number; // Optional for ChartData compatibility
  series?: any[]; // For compatibility with ChartData from report.model.ts
}

@Component({
  selector: 'app-pie-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container" [style.height.px]="height">
      <canvas
        baseChart
        [data]="pieChartData"
        [options]="pieChartOptions"
        [type]="pieChartType">
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
export class PieChartComponent implements AfterViewInit, OnChanges {
  private chartConfig = inject(ChartConfigService);

  @Input() data: PieChartDataPoint[] = [];
  @Input() showLegend = true;
  @Input() doughnut = false;
  @Input() height = 300;
  @Input() legendFontSize = 12;
  @Input() padding: any = 0;

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  pieChartType: 'pie' | 'doughnut' = 'pie';
  pieChartData: ChartData<'pie' | 'doughnut'> = {
    labels: [],
    datasets: []
  };
  pieChartOptions: ChartOptions<'pie' | 'doughnut'> = {};

  ngAfterViewInit(): void {
    this.pieChartType = this.doughnut ? 'doughnut' : 'pie';
    this.updateChart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] || changes['legendFontSize'] || changes['padding']) {
      this.updateChart();
    }
    if (changes['doughnut']) {
      this.pieChartType = this.doughnut ? 'doughnut' : 'pie';
      this.updateChart();
    }
  }

  private updateChart(): void {
    if (!this.data || this.data.length === 0) {
      return;
    }

    // Check if data is ChartData[] format (has 'series' property)
    const isChartDataFormat = this.data.length > 0 && 'series' in this.data[0];

    let labels: string[];
    let values: number[];

    if (isChartDataFormat) {
      // ChartData[] format: flatten series
      labels = (this.data as any[]).map(d => d.name);
      values = (this.data as any[]).map(d => d.series?.[0]?.value || 0);
    } else {
      // Simple format
      labels = this.data.map(d => d.name);
      values = this.data.map(d => d.value || 0);
    }

    this.pieChartData = {
      labels,
      datasets: [{
        data: values,
        backgroundColor: this.chartConfig.chartColors,
        borderColor: '#ffffff',
        borderWidth: 2
      }]
    };

    const optionsMethod = this.doughnut
      ? this.chartConfig.getDoughnutChartOptions.bind(this.chartConfig)
      : this.chartConfig.getPieChartOptions.bind(this.chartConfig);

    this.pieChartOptions = optionsMethod({
      maintainAspectRatio: false,
      layout: {
        padding: this.padding
      },
      plugins: {
        legend: {
          display: this.showLegend,
          position: 'right',
          labels: {
            font: {
              size: this.legendFontSize
            }
          }
        }
      }
    });

    this.chart?.update();
  }
}
