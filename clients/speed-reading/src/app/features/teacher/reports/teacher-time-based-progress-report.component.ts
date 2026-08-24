import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { TeacherReportService } from '../../../core/services/teacher-report.service';
import { AuthService } from '../../../core/services/auth.service';
import { TeacherTimeBasedProgressReport } from '../../../core/models/report.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';

type DateRangePreset = '7days' | '30days' | '90days' | 'thisMonth' | 'thisSemester' | 'custom';

@Component({
  selector: 'app-teacher-time-based-progress-report',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatTooltipModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatInputModule,
    FormsModule,
    LineChartComponent
  ],
  templateUrl: './teacher-time-based-progress-report.component.html',
  styleUrls: ['./teacher-time-based-progress-report.component.scss']
})
export class TeacherTimeBasedProgressReportComponent implements OnInit {
  private teacherReportService = inject(TeacherReportService);
  private authService = inject(AuthService);

  report = signal<TeacherTimeBasedProgressReport | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  // Date Range with signals
  selectedDateRange = signal<DateRangePreset>('30days');
  customStartDate = signal<Date | null>(null);
  customEndDate = signal<Date | null>(null);
  showCustomDatePicker = signal(false);
  maxDate = new Date();

  dateRangeOptions = [
    { value: '7days' as DateRangePreset, label: 'Son 7 Gün', icon: 'today' },
    { value: '30days' as DateRangePreset, label: 'Son 30 Gün', icon: 'date_range' },
    { value: '90days' as DateRangePreset, label: 'Son 90 Gün', icon: 'calendar_month' },
    { value: 'thisMonth' as DateRangePreset, label: 'Bu Ay', icon: 'event' },
    { value: 'thisSemester' as DateRangePreset, label: 'Bu Dönem', icon: 'school' },
    { value: 'custom' as DateRangePreset, label: 'Özel Tarih Aralığı', icon: 'edit_calendar' }
  ];

  // Computed data for charts
  activityChartData = computed(() => {
    const r = this.report();
    return r?.activityIntensityChart || [];
  });

  weeklyChartData = computed(() => {
    const r = this.report();
    return r?.weeklyProgressChart || [];
  });

  improvingStudentsList = computed(() => {
    const r = this.report();
    return r?.improvingStudents || [];
  });

  decliningStudentsList = computed(() => {
    const r = this.report();
    return r?.decliningStudents || [];
  });

  private route = inject(ActivatedRoute);

  // Helper to get effective teacher ID (Param or Current User)
  private get effectiveTeacherId(): string {
    const paramId = this.route.snapshot.queryParamMap.get('teacherId');
    return paramId || this.authService.currentUserValue?.id || '';
  }

  private get currentTeacherId(): string {
    return this.activeTeacherId();
  }

  // Signal to track the ID we are currently viewing
  activeTeacherId = signal<string>('');

  ngOnInit(): void {
    // React into query params changes
    this.route.queryParams.subscribe(params => {
      const tid = params['teacherId'] || this.authService.currentUserValue?.id || '';
      this.activeTeacherId.set(tid);
      this.refreshReport();
    });
  }

  onDateRangeChange(): void {
    if (this.selectedDateRange() === 'custom') {
      this.showCustomDatePicker.set(true);
      if (!this.customStartDate() || !this.customEndDate()) {
        return;
      }
    } else {
      this.showCustomDatePicker.set(false);
    }
    this.refreshReport();
  }

  onCustomDateChange(): void {
    if (this.customStartDate() && this.customEndDate()) {
      this.refreshReport();
    }
  }

  refreshReport(): void {
    const teacherId = this.activeTeacherId();
    if (!teacherId) {
      this.error.set('Öğretmen bilgisi alınamadı.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const { startDate, endDate } = this.calculateDateRange();

    const reportRequest = this.route.snapshot.queryParamMap.has('teacherId')
      ? this.teacherReportService.getAdminTimeBasedProgressReport(teacherId, startDate, endDate)
      : this.teacherReportService.getTimeBasedProgressReport(teacherId, startDate, endDate);

    reportRequest
      .subscribe({
        next: (data) => {
          this.report.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading report:', err);
          this.error.set('Rapor verileri alınırken bir hata oluştu.');
          this.loading.set(false);
        }
      });
  }

  private calculateDateRange(): { startDate: Date, endDate: Date } {
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
        startDate.setDate(1);
        break;
      case 'thisSemester':
        const month = endDate.getMonth();
        if (month >= 8) {
          startDate.setMonth(8, 1);
        } else if (month >= 1) {
          startDate.setMonth(1, 1);
        } else {
          startDate.setFullYear(endDate.getFullYear() - 1);
          startDate.setMonth(8, 1);
        }
        break;
      case 'custom':
        if (this.customStartDate() && this.customEndDate()) {
          return {
            startDate: this.customStartDate()!,
            endDate: this.customEndDate()!
          };
        }
        startDate.setDate(endDate.getDate() - 30);
        break;
      default:
        startDate.setDate(endDate.getDate() - 30);
    }

    return { startDate, endDate };
  }
}
