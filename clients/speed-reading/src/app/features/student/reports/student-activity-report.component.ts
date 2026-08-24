import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentActivityReport } from '../../../core/models/report.model';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { HeatmapComponent } from '../../../shared/components/charts/heatmap.component';

@Component({
  selector: 'app-student-activity-report',
  standalone: true,
  imports: [CommonModule, BarChartComponent, HeatmapComponent],
  templateUrl: './student-activity-report.component.html',
  styleUrls: ['./student-activity-report.component.scss']
})
export class StudentActivityReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  report = signal<StudentActivityReport | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    const studentId = this.authService.currentUserValue?.id ?? '';
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.reportsService.getStudentActivityReport(studentId, startDate, endDate).subscribe({
      next: (data) => { this.report.set(data); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  heatmapData = computed(() => {
    return this.report()?.activityHeatmap.data || [];
  });

  formatMinutes(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }
}
