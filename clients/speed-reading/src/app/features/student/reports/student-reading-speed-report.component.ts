import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentReadingSpeedReport } from '../../../core/models/report.model';
import { GaugeChartComponent } from '../../../shared/components/charts/gauge-chart.component';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { NumberCardComponent } from '../../../shared/components/charts/number-card.component';

@Component({
  selector: 'app-student-reading-speed-report',
  standalone: true,
  imports: [
    CommonModule,
    GaugeChartComponent,
    LineChartComponent,
    BarChartComponent,
    NumberCardComponent
  ],
  templateUrl: './student-reading-speed-report.component.html',
  styleUrls: ['./student-reading-speed-report.component.scss']
})
export class StudentReadingSpeedReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);

  report = signal<StudentReadingSpeedReport | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const studentId = this.authService.currentUserValue?.id ?? '';
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(endDate.getDate() - 30);

    this.reportsService.getStudentReadingSpeedReport(studentId, startDate, endDate).subscribe({
      next: (data) => {
        this.report.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load reading speed report', error);
        this.loading.set(false);
      }
    });
  }

  currentWpmData = computed(() => {
    return [{ name: 'WPM', value: this.report()?.currentWPM || 0 }];
  });

  benchmarkData = computed(() => {
    const report = this.report();
    if (!report) return [];

    return report.wpmBenchmarks.map(b => ({
      name: b.label,
      value: b.value
    }));
  });

  statsDataSource = computed(() => {
    const stats = this.report()?.statistics;
    if (!stats) return [];

    return [
      { metric: 'Average WPM', value: stats.averageWPM.toFixed(0) },
      { metric: 'Median WPM', value: stats.medianWPM.toFixed(0) },
      { metric: 'Min WPM', value: stats.minWPM.toFixed(0) },
      { metric: 'Max WPM', value: stats.maxWPM.toFixed(0) },
      { metric: 'Standard Deviation', value: stats.standardDeviation.toFixed(2) },
      { metric: 'Improvement Rate', value: `${stats.improvementRate.toFixed(1)}%` },
      { metric: 'Total Readings', value: stats.totalReadings.toString() }
    ];
  });
}
