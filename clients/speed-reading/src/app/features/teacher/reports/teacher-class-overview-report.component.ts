import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { TeacherClassOverviewReport } from '../../../core/models/report.model';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';
import { finalize } from 'rxjs/operators';

type DateRangePreset = '7days' | '30days' | '90days' | 'thisMonth' | 'thisSemester' | 'custom';

@Component({
  selector: 'app-teacher-class-overview-report',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatInputModule,
    MatTooltipModule,
    FormsModule,
    BarChartComponent
  ],
  templateUrl: './teacher-class-overview-report.component.html',
  styleUrls: ['./teacher-class-overview-report.component.scss']
})
export class TeacherClassOverviewReportComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);

  report = signal<TeacherClassOverviewReport | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  selectedDateRange = signal<DateRangePreset>('30days');

  // Custom date range
  customStartDate = signal<Date | null>(null);
  customEndDate = signal<Date | null>(null);
  showCustomDatePicker = signal(false);
  maxDate = new Date(); // Today

  dateRangeOptions = [
    { value: '7days' as DateRangePreset, label: 'Son 7 Gün', icon: 'today' },
    { value: '30days' as DateRangePreset, label: 'Son 30 Gün', icon: 'date_range' },
    { value: '90days' as DateRangePreset, label: 'Son 90 Gün', icon: 'calendar_month' },
    { value: 'thisMonth' as DateRangePreset, label: 'Bu Ay', icon: 'event' },
    { value: 'thisSemester' as DateRangePreset, label: 'Bu Dönem', icon: 'school' },
    { value: 'custom' as DateRangePreset, label: 'Özel Tarih Aralığı', icon: 'edit_calendar' }
  ];

  // Computed values for display
  performanceChartData = computed(() => {
    const r = this.report();
    if (!r) return [];
    return [
      { name: 'Ortalamanın Üstü', value: r.studentsAboveAverage },
      { name: 'Ortalama', value: r.studentsAtAverage },
      { name: 'Ortalamanın Altı', value: r.studentsBelowAverage }
    ];
  });

  activitiesPerStudent = computed(() => {
    const r = this.report();
    if (!r || r.totalStudents === 0) return 0;
    return Math.round(r.totalActivitiesCompleted / r.totalStudents);
  });

  private route = inject(ActivatedRoute);

  ngOnInit(): void {
    // Subscribe to query params to reload when teacherId changes
    this.route.queryParams.subscribe(() => {
      this.loadReport();
    });
  }

  loadReport(): void {
    const user = this.authService.currentUserValue;
    const teacherIdParam = this.route.snapshot.queryParamMap.get('teacherId');

    // Use param if available (for Admin view), otherwise current user
    const teacherId = teacherIdParam || user?.id;

    if (!teacherId) {
      this.error.set('Öğretmen bilgisi bulunamadı');
      this.loading.set(false);
      return;
    }

    const { startDate, endDate } = this.getDateRange();
    this.loading.set(true);
    this.error.set(null);

    this.reportsService.getTeacherClassOverviewReport(teacherId, startDate, endDate)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.report.set(data),
        error: (err) => {
          console.error('Error loading class overview:', err);
          this.error.set('Rapor yüklenirken hata oluştu');
        }
      });
  }

  getDateRange(): { startDate: Date; endDate: Date } {
    const endDate = new Date();
    const startDate = new Date();

    switch (this.selectedDateRange()) {
      case '7days':
        startDate.setDate(endDate.getDate() - 7);
        break;
      case '30days':
        startDate.setDate(endDate.getDate() - 30);
        break;
      case '90days':
        startDate.setDate(endDate.getDate() - 90);
        break;
      case 'thisMonth':
        startDate.setDate(1); // First day of current month
        break;
      case 'thisSemester':
        // Academic semester: Sep-Jan or Feb-Jun
        const month = endDate.getMonth();
        if (month >= 8) { // Sep-Dec: Fall semester started in Sep
          startDate.setMonth(8, 1); // September 1st
        } else if (month >= 1) { // Feb-Aug: Spring semester started in Feb
          startDate.setMonth(1, 1); // February 1st
        } else { // January: Still in fall semester from previous year
          startDate.setFullYear(endDate.getFullYear() - 1);
          startDate.setMonth(8, 1); // September 1st of previous year
        }
        break;
      case 'custom':
        if (this.customStartDate() && this.customEndDate()) {
          return {
            startDate: this.customStartDate()!,
            endDate: this.customEndDate()!
          };
        }
        // Fallback to 30 days if custom dates not set
        startDate.setDate(endDate.getDate() - 30);
        break;
    }

    return { startDate, endDate };
  }

  onDateRangeChange(): void {
    if (this.selectedDateRange() === 'custom') {
      this.showCustomDatePicker.set(true);
      // Don't load report until custom dates are selected
      if (!this.customStartDate() || !this.customEndDate()) {
        return;
      }
    } else {
      this.showCustomDatePicker.set(false);
    }
    this.loadReport();
  }

  onCustomDateChange(): void {
    if (this.customStartDate() && this.customEndDate()) {
      this.loadReport();
    }
  }

  refreshReport(): void {
    this.loadReport();
  }
}
