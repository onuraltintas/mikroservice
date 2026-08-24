import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentSeriesReport } from '../../../core/models/report.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { format } from 'date-fns';

@Component({
  selector: 'app-student-series-report',
  standalone: true,
  imports: [CommonModule, LineChartComponent],
  templateUrl: './student-series-report.component.html',
  styleUrls: ['./student-series-report.component.scss']
})
export class StudentSeriesReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  report = signal<StudentSeriesReport | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    const studentId = this.authService.currentUserValue?.id ?? '';
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getStudentSeriesReport(studentId, startDate, endDate).subscribe({
      next: (data) => { this.report.set(data); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  formatDate(date: Date | string | null | undefined): string {
    if (!date) return '';
    const d = new Date(date);
    if (isNaN(d.getTime())) return '';
    return format(d, 'MMM d');
  }
}
