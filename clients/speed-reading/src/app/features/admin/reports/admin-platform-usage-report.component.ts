import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ReportsService } from '../../../core/services/reports.service';
import { AdminPlatformUsageReport } from '../../../core/models/report.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-platform-usage-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatProgressSpinnerModule, LineChartComponent, BarChartComponent],
  templateUrl: './admin-platform-usage-report.component.html',
  styleUrls: ['./admin-platform-usage-report.component.scss']
})
export class AdminPlatformUsageReportComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  report = signal<AdminPlatformUsageReport | null>(null);
  // loading inherited from BaseComponent, but here it's a signal<boolean>(true), BaseComponent has loading = signal<boolean>(false)

  ngOnInit(): void {
    this.loading.set(true);
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getAdminPlatformUsageReport(startDate, endDate)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (data) => { this.report.set(data); },
        error: (err) => {
          this.handleError(err, 'Rapor yüklenirken hata oluştu');
        }
      });
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  getObjectKeys(obj: any): string[] {
    return Object.keys(obj || {});
  }

  getFeatureUsageArray(): { key: string; value: number }[] {
    const stats = this.report()?.featureUsageStats;
    if (!stats) return [];
    return Object.entries(stats).map(([key, value]) => ({ key, value }));
  }

  transformChartData(chartData: any[]): any[] {
    // Backend'den gelen ChartDataDto array'ini olduğu gibi döndür
    // ChartDataDto { name: string, series: ChartSeriesDto[] }
    return chartData || [];
  }
}
