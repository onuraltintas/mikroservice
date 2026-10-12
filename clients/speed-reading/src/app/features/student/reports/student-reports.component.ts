import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { StudentProgramInfo, StudentProgramService } from '../../../core/services/student-program.service';
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
  AssessmentPhasePlanDto,
  AssessmentPhasePlanItemDto,
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
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private programs = inject(StudentProgramService);
  programHistory = signal<StudentProgramInfo[]>([]);
  selectedProgramId = signal<string | null>(null);
  programHistoryError = signal(false);
  private reportRequestVersion = 0;

  dateRangeControl = new FormControl('last7days');
  startDateControl = new FormControl(subDays(new Date(), 7));
  endDateControl = new FormControl(new Date());

  isExporting = signal(false);
  assessmentComparison = signal<AssessmentComparisonDto | null>(null);
  phasePlan = signal<AssessmentPhasePlanDto | null>(null);
  currentStudentId = this.authService.currentUserValue?.id || '';

  ngOnInit(): void {
    this.selectedProgramId.set(this.route.snapshot.queryParamMap.get('programProgressId'));
    this.programs.getMyPrograms().pipe(takeUntil(this.destroy$)).subscribe({ next: history => this.programHistory.set(history),
      error: () => this.programHistoryError.set(true) });
    this.onDateRangeChange();
    this.loadAssessmentComparison();
    this.loadPhasePlan();
  }

  private loadAssessmentComparison(): void {
    const version = this.reportRequestVersion;
    this.assessmentService.getAttemptComparison(this.selectedProgramId() ?? undefined)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: comparison => { if (version === this.reportRequestVersion) this.assessmentComparison.set(comparison); },
        error: () => { if (version === this.reportRequestVersion) this.assessmentComparison.set(null); }
      });
  }

  private loadPhasePlan(): void {
    const version = this.reportRequestVersion;
    this.assessmentService.getPhasePlan(this.selectedProgramId() ?? undefined)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: plan => { if (version === this.reportRequestVersion) this.phasePlan.set(plan); },
        error: () => { if (version === this.reportRequestVersion) this.phasePlan.set(null); }
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

  selectProgram(programId: string): void {
    this.reportRequestVersion++;
    this.selectedProgramId.set(programId || null);
    this.assessmentComparison.set(null);
    this.phasePlan.set(null);
    this.loadAssessmentComparison();
    this.loadPhasePlan();
  }

  selectedProgram(): StudentProgramInfo | null {
    return this.programHistory().find(program => program.progressId === this.selectedProgramId())
      ?? (!this.selectedProgramId() ? this.programHistory()[0] : null) ?? null;
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

  phaseStatusLabel(status: number): string {
    switch (status) {
      case 1: return 'Kilitli';
      case 2: return 'Hazır';
      case 3: return 'Devam ediyor';
      case 4: return 'Tamamlandı';
      default: return 'Beklemede';
    }
  }

  phaseStatusClass(status: number): string {
    switch (status) {
      case 2: return 'available';
      case 3: return 'in-progress';
      case 4: return 'completed';
      default: return 'locked';
    }
  }

  startAssessment(phase: AssessmentPhasePlanItemDto): void {
    if (phase.phase !== this.phasePlan()?.nextPhase
      || (phase.status !== 2 && phase.status !== 3)) {
      return;
    }

    this.router.navigate(['/student/assessment'], {
      queryParams: { phase: phase.phase, ...(this.selectedProgramId() ? { programProgressId: this.selectedProgramId() } : {}) }
    });
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
