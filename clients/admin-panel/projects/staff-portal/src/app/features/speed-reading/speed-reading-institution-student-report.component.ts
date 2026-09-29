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
  SpeedReadingInstitutionMember,
  SpeedReadingInstitutionService,
  SpeedReadingInstitutionStudentReport,
} from './speed-reading-institution.service';

@Component({
  selector: 'staff-speed-reading-institution-student-report',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './speed-reading-institution-student-report.component.html',
  styleUrl: './speed-reading-institution-student-report.component.scss',
})
export class SpeedReadingInstitutionStudentReportComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) institutionId!: string;
  @Input({ required: true }) student!: SpeedReadingInstitutionMember;
  @Output() closed = new EventEmitter<void>();

  private readonly service = inject(SpeedReadingInstitutionService);
  readonly report = signal<SpeedReadingInstitutionStudentReport | null>(null);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  private reportSubscription: Subscription | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['institutionId'] || changes['student']) && this.institutionId && this.student) {
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
    this.reportSubscription = this.service
      .getStudentReport(this.institutionId, this.student.userId, dateFrom, dateTo)
      .subscribe({
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
    return 'Öğrenci raporu yüklenemedi. Kurum kapsamını kontrol edip tekrar deneyin.';
  }
}
