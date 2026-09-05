import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartOptions } from 'chart.js';
import { ChartConfigService } from '../../../core/services/chart-config.service';

export interface GaugeChartData {
  name: string;
  value: number;
  max?: number;
}

@Component({
  selector: 'app-gauge-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container">
      <canvas
        baseChart
        role="img"
        [attr.aria-label]="label + ': ' + currentValue"
        [data]="gaugeChartData"
        [options]="gaugeChartOptions"
        [type]="'doughnut'">
      </canvas>
      <div class="gauge-value">
        <div class="value">{{ currentValue }}</div>
        <div class="label">{{ label }}</div>
      </div>
    </div>
  `,
  styles: [`
    .chart-container {
      position: relative;
      height: 200px;
      width: 200px;
      margin: 0 auto;
    }
    .gauge-value {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      text-align: center;
    }
    .value {
      font-size: 32px;
      font-weight: bold;
      color: var(--ui-text, var(--sp-text-1, #1e1b4b));
    }
    .label {
      font-size: 12px;
      color: var(--ui-text-subtle, var(--sp-text-3, #6b7280));
      margin-top: 4px;
    }
  `]
})
export class GaugeChartComponent implements AfterViewInit, OnChanges {
  private chartConfig = inject(ChartConfigService);

  @Input() value: number = 0;
  @Input() min: number = 0;
  @Input() max: number = 100;
  @Input() label: string = '';
  @Input() unit: string = '';
  @Input() units: string = ''; // Alias for unit

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  gaugeChartData: ChartData<'doughnut'> = {
    labels: ['Value', 'Remaining'],
    datasets: []
  };
  gaugeChartOptions: ChartOptions<'doughnut'> = {};

  get currentValue(): string {
    return `${Math.round(this.value)}${this.unit || this.units}`;
  }

  ngAfterViewInit(): void {
    this.updateChart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['value'] || changes['max']) && !changes['value']?.firstChange) {
      this.updateChart();
    }
  }

  private updateChart(): void {
    const percentage = this.max > 0
      ? Math.min(100, Math.max(0, (this.value / this.max) * 100))
      : 0;
    const remaining = 100 - percentage;

    this.gaugeChartData = {
      labels: ['Value', 'Remaining'],
      datasets: [{
        data: [percentage, remaining],
        backgroundColor: [
          this.chartConfig.colors.primary,
          this.chartConfig.colors.neutral
        ],
        borderWidth: 0,
        circumference: 180,
        rotation: 270
      }]
    };

    this.gaugeChartOptions = {
      responsive: true,
      maintainAspectRatio: true,
      plugins: {
        legend: {
          display: false
        },
        tooltip: {
          enabled: false
        }
      }
    };

    this.chart?.update();
  }
}
