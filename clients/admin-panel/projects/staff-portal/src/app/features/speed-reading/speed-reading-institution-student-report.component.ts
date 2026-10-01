import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { Subscription } from 'rxjs';
import {
  SpeedReadingReportStudent,
  SpeedReadingInstitutionService,
  SpeedReadingStudentReport,
} from './speed-reading-institution.service';
import { SpeedReadingTeacherService } from './speed-reading-teacher.service';
import { ProgramRecommendationPanelComponent } from './program-recommendation-panel.component';

@Component({
  selector: 'staff-speed-reading-student-report',
  standalone: true,
  imports: [CommonModule, ProgramRecommendationPanelComponent],
  templateUrl: './speed-reading-institution-student-report.component.html',
  styleUrl: './speed-reading-institution-student-report.component.scss',
})
export class SpeedReadingStudentReportComponent implements OnChanges, OnDestroy {
  @Input() institutionId: string | null = null;
  @Input() teacherScoped = false;
  @Input({ required: true }) student!: SpeedReadingReportStudent;
  @Output() closed = new EventEmitter<void>();

  private readonly institutionService = inject(SpeedReadingInstitutionService);
  private readonly teacherService = inject(SpeedReadingTeacherService);
  readonly report = signal<SpeedReadingStudentReport | null>(null);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  private reportSubscription: Subscription | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (
      (changes['institutionId'] || changes['teacherScoped'] || changes['student']) &&
      this.student &&
      (this.teacherScoped || this.institutionId)
    ) {
      this.loadReport();
    }
  }

  ngOnDestroy(): void {
    this.reportSubscription?.unsubscribe();
  }

  loadReport(): void {
    this.reportSubscription?.unsubscribe();
    const dateTo = new Date();
    const dateFrom = new Date(dateTo);
    dateFrom.setUTCDate(dateFrom.getUTCDate() - 29);
    dateFrom.setUTCHours(0, 0, 0, 0);
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const reportRequest = this.teacherScoped
      ? this.teacherService.getStudentReport(this.student.userId, dateFrom, dateTo)
      : this.institutionId
        ? this.institutionService.getStudentReport(
            this.institutionId,
            this.student.userId,
            dateFrom,
            dateTo,
          )
        : null;
    if (!reportRequest) {
      this.errorMessage.set('Öğrenci raporu için geçerli bir erişim kapsamı bulunamadı.');
      this.isLoading.set(false);
      return;
    }
    this.reportSubscription = reportRequest.subscribe({
      next: (report) => this.report.set(report),
      error: (error) => {
        this.errorMessage.set(this.getErrorMessage(error));
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false),
    });
  }

  formatNumber(value: number, fractionDigits = 0): string {
    return new Intl.NumberFormat('tr-TR', {
      maximumFractionDigits: fractionDigits,
      minimumFractionDigits: fractionDigits,
    }).format(value);
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  }

  formatDuration(seconds: number): string {
    const minutes = Math.round(seconds / 60);
    return minutes > 0 ? `${minutes} dk` : `${seconds} sn`;
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
      const body = error.error as { message?: unknown; description?: unknown };
      if (typeof body.message === 'string') return body.message;
      if (typeof body.description === 'string') return body.description;
    }
    return this.teacherScoped
      ? 'Öğrenci raporu yüklenemedi. Öğretmen-öğrenci bağlantısını kontrol edip tekrar deneyin.'
      : 'Öğrenci raporu yüklenemedi. Kurum kapsamını kontrol edip tekrar deneyin.';
  }
}
