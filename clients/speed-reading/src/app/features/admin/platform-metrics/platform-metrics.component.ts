import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { PlatformMetricsService, PlatformMetricsSummary, PlatformMetrics } from '../../../core/services/platform-metrics.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-platform-metrics',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatFormFieldModule,
    MatInputModule,
    FormsModule
  ],
  templateUrl: './platform-metrics.component.html',
  styleUrls: ['./platform-metrics.component.scss']
})
export class PlatformMetricsComponent extends BaseComponent implements OnInit {
  metrics: PlatformMetricsSummary | null = null;
  reversedDailyMetrics: PlatformMetrics[] = [];
  isCollecting = false;
  error: string | null = null;
  selectedPeriod: string = '30';

  constructor(private metricsService: PlatformMetricsService) {
    super();
  }

  ngOnInit(): void {
    this.loadMetrics();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadMetrics(): void {
    this.loading.set(true);
    this.error = null;

    const days = this.selectedPeriod === '7' ? 7 : this.selectedPeriod === '90' ? 90 : 30;
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);
    const observable = this.metricsService.getMetrics(startDate, endDate);

    observable.pipe(
      takeUntil(this.destroy$),
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (data) => {
        this.metrics = data;
        this.reversedDailyMetrics = [...(data.dailyMetrics ?? [])].reverse();
      },
      error: (err) => {
        this.error = 'Metrikler yüklenirken hata oluştu';
        console.error('Error loading metrics:', err);
      }
    });
  }

  onPeriodChange(): void {
    if (this.selectedPeriod !== 'custom') {
      this.loadMetrics();
    }
  }

  collectMetrics(): void {
    this.isCollecting = true;

    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);

    this.metricsService.collectMetrics(yesterday).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: () => {
        this.isCollecting = false;
        this.loadMetrics();
      },
      error: (err) => {
        this.isCollecting = false;
        this.error = 'Metrikler toplanırken hata oluştu';
        console.error('Error collecting metrics:', err);
      }
    });
  }

  getDayCount(): number {
    return this.metrics?.dailyMetrics?.length || 1;
  }
}
