import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { TeacherReportService } from '../../../core/services/teacher-report.service';
import { TeacherContentAnalysisReport, ChartData } from '../../../core/models/report.model';
import { BarChartComponent } from '../../../shared/components/charts/bar-chart.component';

type DateRangePreset = '7days' | '30days' | '90days' | 'thisMonth' | 'thisSemester' | 'custom';

@Component({
  selector: 'app-teacher-content-analysis-report',
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
    MatTabsModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatInputModule,
    FormsModule,
    BarChartComponent
  ],
  templateUrl: './teacher-content-analysis-report.component.html',
  styleUrls: ['./teacher-content-analysis-report.component.scss']
})
export class TeacherContentAnalysisReportComponent implements OnInit {
  private teacherReportService = inject(TeacherReportService);
  private authService = inject(AuthService);

  report = signal<TeacherContentAnalysisReport | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  // Date Range Logic with signals
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

  // Computed Chart Data
  exerciseFrequencyChart = computed(() => {
    const r = this.report();
    return r?.exerciseFrequencyChart || [];
  });

  readingPerformanceChart = computed(() => {
    const r = this.report();
    return r?.readingPerformanceChart || [];
  });

  exerciseList = computed(() => {
    return this.report()?.exerciseAnalysis || [];
  });

  readingList = computed(() => {
    return this.report()?.readingAnalysis || [];
  });

  // Get current teacher ID
  private get currentTeacherId(): string {
    return this.activeTeacherId();
  }

  private route = inject(ActivatedRoute);
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
      this.error.set('Öğretmen kimliği bulunamadı.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const { startDate, endDate } = this.calculateDateRange();

    this.teacherReportService.getContentAnalysisReport(teacherId, startDate, endDate)
      .subscribe({
        next: (data) => {
          this.report.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading report:', err);
          this.error.set('Rapor yüklenirken bir hata oluştu.');
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

