import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { MeasuredProgress, MeasuredProgressService } from '../../../core/services/measured-progress.service';
import { trPercent } from '../../../../../../shared/utils/tr-number';
import { StudentDashboardReport } from '../../../core/models/report.model';
import { format } from 'date-fns';
import { tr } from 'date-fns/locale';
import { speedReadingReportLabel } from '../../../../../../shared/utils/speed-reading-report-labels';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';

import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-student-dashboard-report',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LineChartComponent,
    BarChartComponent
  ],
  templateUrl: './student-dashboard-report.component.html',
  styleUrls: ['./student-dashboard-report.component.scss']
})
export class StudentDashboardReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);

  private measuredProgress = inject(MeasuredProgressService);

  report = signal<StudentDashboardReport | null>(null);
  loading = signal(true);
  /** Same measured values the home page shows (live test 19.11.2026, A3). */
  measured = signal<MeasuredProgress | null>(null);
  readingSpeed = computed(() => this.measured()?.readingSpeed
    ?? ((this.report()?.currentWPM ?? 0) > 0 ? Math.round(this.report()!.currentWPM!) : null));
  comprehension = computed(() => {
    const value = this.measured()?.comprehension;
    if (value != null) return Math.round(value);
    return this.report()?.comprehensionDataAvailable ? Math.round(this.report()?.averageComprehension ?? 0) : null;
  });

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

    this.measuredProgress.load().subscribe(value => this.measured.set(value));

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

  pct(value: number | null | undefined, decimals = 0): string { return trPercent(value, decimals); }

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

  /** Last 30 measured readings, so the line stays readable. */
  wpmProgressData = computed(() => {
    const points = (this.report()?.wpmProgress ?? []).slice(-30);
    if (points.length === 0) return [];
    return [{
      name: 'Okuma hızı',
      series: points.map(point => ({
          name: format(new Date(point.name + 'T00:00:00'), 'd MMM', { locale: tr }),
          value: point.series[0].value
        }))
    }];
  });

  speedPoints = computed(() => this.wpmProgressData()[0]?.series.length ?? 0);

  /** Last 12 comprehension results as columns (0–100). */
  comprehensionColumns = computed(() => (this.report()?.comprehensionTrend ?? []).slice(-12).map(point => ({
    name: format(new Date(point.name + 'T00:00:00'), 'd MMM', { locale: tr }),
    value: Math.round(point.series[0].value)
  })));

  /** Reading vs exercise counts for the split bar. */
  activitySplit = computed(() => {
    const report = this.report();
    const reading = report?.readingSessions ?? 0;
    const exercise = report?.exercisesCompleted ?? 0;
    return reading + exercise > 0 ? { reading, exercise } : null;
  });

  /** Study calendar: 12 weeks (Monday first) from the daily activity series; level 0–4 relative to the busiest day. */
  studyCalendar = computed(() => {
    const byDate = new Map((this.report()?.activityTrend ?? []).map(point => [point.name, point.series?.[0]?.value ?? 0]));
    const max = Math.max(1, ...byDate.values());
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const monday = new Date(today); monday.setDate(today.getDate() - ((today.getDay() + 6) % 7) - 11 * 7);
    const weeks: { date: string; level: number; future: boolean; title: string }[][] = [];
    for (let w = 0; w < 12; w++) {
      const week = [];
      for (let d = 0; d < 7; d++) {
        const day = new Date(monday); day.setDate(monday.getDate() + w * 7 + d);
        const key = format(day, 'yyyy-MM-dd');
        const value = byDate.get(key) ?? 0;
        const level = value <= 0 ? 0 : Math.min(4, Math.ceil((value / max) * 4));
        week.push({ date: key, level, future: day > today,
          title: `${format(day, 'd MMMM', { locale: tr })}: ${value} etkinlik` });
      }
      weeks.push(week);
    }
    return weeks;
  });

  calendarLabel = computed(() => {
    const days = this.studyCalendar().flat().filter(day => !day.future);
    const active = days.filter(day => day.level > 0).length;
    return `Çalışma takvimi: son 12 haftada ${days.length} günün ${active} gününde çalıştın.`;
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
