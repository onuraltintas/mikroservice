import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule } from '@angular/material/sort';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ReportsService } from '../../../core/services/reports.service';
import { AdminInstitutionReport } from '../../../core/models/report.model';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-institution-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatTableModule, MatSortModule, MatProgressSpinnerModule, BarChartComponent],
  templateUrl: './admin-institution-report.component.html',
  styleUrls: ['./admin-institution-report.component.scss']
})
export class AdminInstitutionReportComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  report = signal<AdminInstitutionReport | null>(null);
  // loading inherited from BaseComponent, but here it's a signal<boolean>(true), BaseComponent has loading = signal<boolean>(false)
  // We can use the inherited one but we need to set it to true initially or just use it as is.
  // BaseComponent loading is signal<boolean>(false).
  // Let's use the inherited one and set it to true in ngOnInit if needed, or just use a separate one if logic differs.
  // Actually, BaseComponent has `loading = signal<boolean>(false)`.
  // This component initializes it to true.
  // I will use the inherited one and set it to true in ngOnInit.

  ngOnInit(): void {
    this.loading.set(true);
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getAdminInstitutionReport(startDate, endDate)
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

  getChartData(): any[] {
    return this.report()?.institutionComparison?.map(i => ({ name: i.institutionName, value: i.totalUsers })) || [];
  }
}
