import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { ReportsService } from '../../../core/services/reports.service';
import { AdminContentAnalysisReport } from '../../../core/models/report.model';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-content-analysis-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatTableModule, MatProgressSpinnerModule, MatTabsModule, MatIconModule, BarChartComponent],
  templateUrl: './admin-content-analysis-report.component.html',
  styleUrls: ['./admin-content-analysis-report.component.scss']
})
export class AdminContentAnalysisReportComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  report = signal<AdminContentAnalysisReport | null>(null);

  readingList = computed(() => this.report()?.readingAnalysis || []);
  exerciseList = computed(() => this.report()?.exerciseAnalysis || []);
  readingPerformanceChart = computed(() => this.report()?.readingPerformanceChart || []);
  exerciseFrequencyChart = computed(() => this.report()?.exerciseFrequencyChart || []);

  ngOnInit(): void {
    this.loading.set(true);
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getAdminContentAnalysisReport(startDate, endDate)
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
}
