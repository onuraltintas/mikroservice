import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { ReportsService } from '../../../core/services/reports.service';
import { AdminSystemHealthReport } from '../../../core/models/report.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { GaugeChartComponent } from '../../../shared/components/charts/gauge-chart.component';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-system-health-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatProgressSpinnerModule, MatTableModule, MatChipsModule, LineChartComponent, GaugeChartComponent],
  templateUrl: './admin-system-health-report.component.html',
  styleUrls: ['./admin-system-health-report.component.scss']
})
export class AdminSystemHealthReportComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  report = signal<AdminSystemHealthReport | null>(null);
  // loading inherited from BaseComponent, but here it's a signal<boolean>(true), BaseComponent has loading = signal<boolean>(false)

  ngOnInit(): void {
    this.loading.set(true);
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getAdminSystemHealthReport(startDate, endDate)
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

  getHealthScoreData(): any[] {
    return [{ name: 'Health', value: this.report()?.overallHealthScore || 0 }];
  }

  transformChartData(chartData: any[]): any[] {
    // Backend ChartDataDto formatını LineChart'ın beklediği formata çevir
    return chartData || [];
  }
}
