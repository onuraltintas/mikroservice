import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { subDays, subMonths } from 'date-fns';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil } from 'rxjs/operators';
import { StudentDashboardReportComponent } from './student-dashboard-report.component';
import {
  AssessmentComparisonDto,
  AssessmentComparisonPointDto,
  AssessmentService
} from '../../../services/assessment.service';

@Component({
  selector: 'app-student-reports',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, StudentDashboardReportComponent],
  templateUrl: './student-reports.component.html',
  styleUrls: ['./student-reports.component.scss']
})
export class StudentReportsComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  private assessmentService = inject(AssessmentService);

  dateRangeControl = new FormControl('last7days');
  startDateControl = new FormControl(subDays(new Date(), 7));
  endDateControl = new FormControl(new Date());

  isExporting = signal(false);
  assessmentComparison = signal<AssessmentComparisonDto | null>(null);
  currentStudentId = this.authService.currentUserValue?.id || '';

  ngOnInit(): void {
    this.onDateRangeChange();
    this.loadAssessmentComparison();
  }

  private loadAssessmentComparison(): void {
    this.assessmentService.getAttemptComparison()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: comparison => this.assessmentComparison.set(comparison),
        error: () => this.assessmentComparison.set(null)
      });
  }

  phaseLabel(phase: number): string {
    switch (phase) {
      case 1: return 'Başlangıç';
      case 2: return 'Eğitim sonrası';
      case 3: return 'Kalıcılık';
      case 4: return 'Transfer';
      default: return 'Değerlendirme';
    }
  }

  metric(value: number | null | undefined, suffix = ''): string {
    return value === null || value === undefined ? '—' : `${Math.round(value)}${suffix}`;
  }

  delta(value: number | null | undefined, suffix = ''): string {
    if (value === null || value === undefined) return '—';
    const sign = value > 0 ? '+' : '';
    return `${sign}${Math.round(value)}${suffix}`;
  }

  deltaClass(value: number | null | undefined): string {
    if (value === null || value === undefined || value === 0) return 'neutral';
    return value > 0 ? 'positive' : 'negative';
  }

  trackAttempt(_: number, attempt: AssessmentComparisonPointDto): string {
    return attempt.attemptId;
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  onDateRangeChange(): void {
    const range = this.dateRangeControl.value;
    const now = new Date();

    switch (range) {
      case 'last7days':
        this.startDateControl.setValue(subDays(now, 7));
        this.endDateControl.setValue(now);
        break;
      case 'last30days':
        this.startDateControl.setValue(subDays(now, 30));
        this.endDateControl.setValue(now);
        break;
      case 'last3months':
        this.startDateControl.setValue(subMonths(now, 3));
        this.endDateControl.setValue(now);
        break;
    }
  }


}
