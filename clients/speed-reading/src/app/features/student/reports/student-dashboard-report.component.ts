import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { StudentDashboardReport } from '../../../core/models/report.model';
import { format } from 'date-fns';
import { tr } from 'date-fns/locale';
import { speedReadingReportLabel } from '../../../../../../shared/utils/speed-reading-report-labels';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { PieChartComponent } from '../../../shared/components/charts/pie-chart.component';

import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-student-dashboard-report',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LineChartComponent,
    PieChartComponent
  ],
  templateUrl: './student-dashboard-report.component.html',
  styleUrls: ['./student-dashboard-report.component.scss']
})
export class StudentDashboardReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);

  report = signal<StudentDashboardReport | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const currentUser = this.authService.currentUserValue;
    if (!currentUser) {
      console.error('No authenticated user found');
      this.loading.set(false);
      return;
    }

    const studentId = currentUser.id;
    const endDate = new Date();
    // The central analytics endpoint intentionally caps the range at 366 days.
    const startDate = new Date(endDate.getTime() - 366 * 24 * 60 * 60 * 1000);

    // Load main report
    this.reportsService.getStudentDashboardReport(studentId, startDate, endDate).subscribe({
      next: (data) => {
        this.report.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load dashboard report', error);
        this.loading.set(false);
      }
    });

  }

  // Transform pie chart data - pie charts need {name, value} format
  pieChartData = computed(() => {
    const data = this.report()?.activityDistribution;
    if (!data || data.length === 0) return [];

    // Flatten: take first series value from each item
    return data.map(item => ({
      name: speedReadingReportLabel(item.name),
      value: item.series?.[0]?.value || 0
    }));
  });

  // Transform line chart data - line charts need [{name: seriesName, series: [{name: x, value: y}]}]
  activityTrendData = computed(() => {
    const data = this.report()?.activityTrend;
    if (!data || data.length === 0) return [];

    // Transform from backend format to chart format
    return [{
      name: 'Aktiviteler',
      series: data.map(item => ({
        name: item.name,
        value: item.series?.[0]?.value || 0
      }))
    }];
  });

  wpmProgressData = computed(() => {
    const points = this.report()?.wpmProgress ?? [];
    if (points.length === 0) return [];
    return [{
      name: 'WPM',
      series: points.map(point => ({
          name: format(new Date(point.name + 'T00:00:00'), 'd MMM', { locale: tr }),
          value: point.series[0].value
        }))
    }];
  });

  comprehensionTrendData = computed(() => {
    const points = this.report()?.comprehensionTrend ?? [];
    if (points.length === 0) return [];
    return [{
      name: 'Anlama Oranı',
      series: points.map(point => ({
          name: format(new Date(point.name + 'T00:00:00'), 'd MMM', { locale: tr }),
          value: point.series[0].value
        }))
    }];
  });

  getMilestoneIcon(type: string): string {
    const icons: Record<string, string> = {
      speed: 'speed',
      comprehension: 'psychology',
      streak: 'local_fire_department',
      completion: 'check_circle',
      achievement: 'emoji_events'
    };
    return icons[type] || 'star';
  }

  formatDate(date: Date | string | null | undefined): string {
    if (!date) return '';
    const d = new Date(date);
    if (isNaN(d.getTime())) return '';
    return format(d, 'd MMM yyyy', { locale: tr });
  }
}
