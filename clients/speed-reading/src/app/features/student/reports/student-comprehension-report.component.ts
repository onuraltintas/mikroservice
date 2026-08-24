import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentComprehensionReport } from '../../../core/models/report.model';
import { NumberCardComponent } from '../../../shared/components/charts/number-card.component';
import { RadarChartComponent } from '../../../shared/components/charts/radar-chart.component';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';

@Component({
  selector: 'app-student-comprehension-report',
  standalone: true,
  imports: [
    CommonModule,
    NumberCardComponent,
    RadarChartComponent,
    BarChartComponent
  ],
  templateUrl: './student-comprehension-report.component.html',
  styleUrls: ['./student-comprehension-report.component.scss']
})
export class StudentComprehensionReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  report = signal<StudentComprehensionReport | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    const studentId = this.authService.currentUserValue?.id ?? '';
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);

    this.reportsService.getStudentComprehensionReport(studentId, startDate, endDate).subscribe({
      next: (data) => { this.report.set(data); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  benchmarkData = computed(() => {
    return this.report()?.comprehensionBenchmarks?.map(b => ({ name: b.label, value: b.value })) || [];
  });

  overallComprehensionData = computed(() => {
    return [{ name: 'Comprehension', value: this.report()?.overallComprehension || 0 }];
  });
}
