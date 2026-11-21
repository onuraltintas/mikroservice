import { Component, Input, ViewChild, AfterViewInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartOptions } from 'chart.js';
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
        role="img"
        [attr.aria-label]="chartLabel + (valueLabel ? ': ' + valueLabel : '')"
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
  @Input() chartLabel = 'Çubuk grafiği';
  /** Brand colour key from ChartConfigService.colors (primary, info, sand, purple, teal...). */
  @Input() color?: string;
  /** Bars below the target are drawn as a soft tint. */
  @Input() target?: number | null;
  @Input() suggestedMax?: number;
  @Input() unit = '';
  valueLabel = '';

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

    const c = this.chartConfig.colors;
    const base = this.color ? (c as Record<string, string>)[this.color] ?? c.primary : c.primary;
    this.barChartData = {
      labels,
      datasets: [{
        label: this.yAxisLabel || 'Değer',
        data: values,
        // Bars at or above the target wear the full colour; the rest a soft tint of it.
        backgroundColor: values.map(v => this.target == null || v >= this.target ? base : this.chartConfig.withAlpha(base, 0.35)),
        hoverBackgroundColor: base,
        borderRadius: 8,
        borderSkipped: false,
        maxBarThickness: this.horizontal ? 22 : 34
      }]
    };
    const axes = this.chartConfig.axes(true) as any;
    const title = (text: string) => ({ display: !!text, text, color: c.muted,
      font: { family: this.chartConfig.fontFamily, size: 12, weight: 'bold' as const } });
    const valueAxis = { ...axes.y, title: title(this.horizontal ? this.xAxisLabel : this.yAxisLabel), suggestedMax: this.suggestedMax };
    const categoryAxis = { ...axes.x, title: title(this.horizontal ? this.yAxisLabel : this.xAxisLabel),
      ticks: { ...axes.x.ticks, maxRotation: 0, autoSkip: true, maxTicksLimit: this.horizontal ? 20 : 10 } };
    this.barChartOptions = this.chartConfig.getBarChartOptions({
      indexAxis: this.horizontal ? 'y' : 'x',
      maintainAspectRatio: false,
      plugins: { legend: { display: this.showLegend } },
      scales: this.horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis }
    });
    this.valueLabel = labels.map((label, i) => `${label} ${Math.round(values[i])}${this.unit}`).join(', ');
    this.chart?.update();
  }
}
