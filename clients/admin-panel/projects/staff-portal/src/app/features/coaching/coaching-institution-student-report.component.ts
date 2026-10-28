import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CoachingStaffStudyComponent } from './coaching-staff-study.component';
import { CoachingFinanceComponent } from '../../../../../../src/app/features/coaching-portal/pages/coaching-finance.component';
import { finalize } from 'rxjs';
import { CoachingLabelPipe } from '../../../../../../src/app/features/coaching-portal/coaching-labels';
import {
  CoachingInstitutionHistoryType,
  CoachingInstitutionService,
  CoachingInstitutionStudent,
  CoachingInstitutionStudentDetail,
  CoachingInstitutionStudentHistoryItem
} from './coaching-institution.service';

@Component({
  selector: 'staff-coaching-institution-student-report',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, CoachingStaffStudyComponent, CoachingFinanceComponent],
  templateUrl: './coaching-institution-student-report.component.html',
  styleUrl: './coaching-institution-student-report.component.scss'
})
export class CoachingInstitutionStudentReportComponent implements OnInit {
  private readonly institutionService = inject(CoachingInstitutionService);
  private readonly destroyRef = inject(DestroyRef);
  private detailRequestId = 0;
  private historyRequestId = 0;

  @Input({ required: true }) student!: CoachingInstitutionStudent;
  @Output() readonly back = new EventEmitter<void>();
  @ViewChild(CoachingStaffStudyComponent) private study?: CoachingStaffStudyComponent;
  @ViewChild(CoachingFinanceComponent) private finance?: CoachingFinanceComponent;
  readonly financeVisible = signal(false);
  async canLeavePage(): Promise<boolean> { return await this.finance?.canLeavePage() ?? true; }
  async toggleFinance(): Promise<void> {
    if (this.financeVisible() && !await this.canLeavePage()) return;
    this.financeVisible.set(!this.financeVisible());
  }

  readonly detail = signal<CoachingInstitutionStudentDetail | null>(null);
  readonly studyVisible = signal(false);
  readonly briefMode = signal(false);
  toggleStudy(): void {
    if (this.briefMode()) { this.briefMode.set(false); this.studyVisible.set(true); }
    else this.studyVisible.set(!this.studyVisible());
  }
  openSessionBrief(): void {
    if (this.briefMode()) this.study?.refreshBrief();
    this.briefMode.set(true); this.studyVisible.set(true); this.loadDetail();
  }
  readonly history = signal<CoachingInstitutionStudentHistoryItem[]>([]);
  readonly historyTypes: CoachingInstitutionHistoryType[] = ['Assignments', 'Exams', 'Sessions', 'Goals'];
  readonly historyLabels: Record<CoachingInstitutionHistoryType, string> = {
    Assignments: 'Ödevler', Exams: 'Sınavlar', Sessions: 'Seanslar', Goals: 'Hedefler'
  };
  readonly statusOptions: Record<CoachingInstitutionHistoryType, { value: string; label: string }[]> = {
    Assignments: [
      { value: 'Assigned', label: 'Atandı' }, { value: 'InProgress', label: 'Devam ediyor' },
      { value: 'Submitted', label: 'Teslim edildi' }, { value: 'Graded', label: 'Değerlendirildi' },
      { value: 'Cancelled', label: 'İptal edildi' }
    ],
    Exams: [{ value: 'Result', label: 'Sonuç kaydı' }],
    Sessions: [
      { value: 'NotRecorded', label: 'Katılım işlenmedi' }, { value: 'Present', label: 'Katıldı' },
      { value: 'Absent', label: 'Katılmadı' }, { value: 'Late', label: 'Geç katıldı' },
      { value: 'Excused', label: 'Mazeretli' }, { value: 'Cancelled', label: 'İptal edildi' }
    ],
    Goals: [{ value: 'InProgress', label: 'Devam ediyor' }, { value: 'Completed', label: 'Tamamlandı' }]
  };
  readonly historyType = signal<CoachingInstitutionHistoryType>('Assignments');
  readonly historyPage = signal(1);
  readonly historyTotalPages = signal(1);
  readonly historyTotalCount = signal(0);
  readonly isDetailLoading = signal(true);
  readonly detailError = signal<string | null>(null);
  readonly isHistoryLoading = signal(false);
  readonly historyError = signal<string | null>(null);
  historySearch = '';
  historyStatus = '';
  historyFromDate = '';
  historyToDate = '';

  ngOnInit(): void {
    this.loadDetail();
    this.loadHistory('Assignments', 1);
  }

  loadDetail(): void {
    const requestId = ++this.detailRequestId;
    this.isDetailLoading.set(true);
    this.detailError.set(null);
    this.institutionService.getStudentDetail(this.student.userId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        if (requestId === this.detailRequestId) this.isDetailLoading.set(false);
      })
    ).subscribe({
      next: detail => {
        if (requestId === this.detailRequestId) this.detail.set(detail);
      },
      error: () => {
        if (requestId === this.detailRequestId) {
          this.detailError.set('Öğrenci özeti yüklenemedi. Öğrencinin kurum kaydını ve bağlantınızı kontrol edip tekrar deneyin.');
        }
      }
    });
  }

  selectHistoryType(type: CoachingInstitutionHistoryType): void {
    this.historyType.set(type);
    this.historyStatus = '';
    this.loadHistory(type, 1);
  }

  applyFilters(): void {
    if (!this.validateHistoryRange()) return;
    this.loadHistory(this.historyType(), 1);
  }

  retryDetail(): void {
    this.loadDetail();
  }

  retryHistory(): void {
    this.loadHistory(this.historyType(), this.historyPage());
  }

  previousPage(): void {
    if (this.historyPage() > 1) this.loadHistory(this.historyType(), this.historyPage() - 1);
  }

  nextPage(): void {
    if (this.historyPage() < this.historyTotalPages()) {
      this.loadHistory(this.historyType(), this.historyPage() + 1);
    }
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
    }).format(date);
  }

  statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Assigned: 'Atandı', InProgress: 'Devam ediyor', Submitted: 'Teslim edildi', Graded: 'Değerlendirildi',
      Result: 'Sonuç kaydı', NotRecorded: 'Katılım işlenmedi', Present: 'Katıldı', Absent: 'Katılmadı',
      Late: 'Geç katıldı', Excused: 'Mazeretli', Cancelled: 'İptal edildi', Completed: 'Tamamlandı'
    };
    return labels[status] ?? status;
  }

  trackById(_: number, item: CoachingInstitutionStudentHistoryItem): string {
    return `${item.type}:${item.id}`;
  }

  private historyFilter() {
    return {
      pageNumber: 1,
      pageSize: 25,
      ...(this.historyFromDate ? { fromDate: this.localDayBoundary(this.historyFromDate, false) } : {}),
      ...(this.historyToDate ? { toDate: this.localDayBoundary(this.historyToDate, true) } : {}),
      ...(this.historyStatus ? { status: this.historyStatus } : {}),
      ...(this.historySearch.trim() ? { search: this.historySearch.trim().slice(0, 100) } : {})
    };
  }

  private localDayBoundary(value: string, end: boolean): string {
    const [year, month, day] = value.split('-').map(Number);
    const boundary = new Date(year, month - 1, day + (end ? 1 : 0));
    return new Date(boundary.getTime() - (end ? 1 : 0)).toISOString();
  }

  private validateHistoryRange(): boolean {
    if (this.historyFromDate && this.historyToDate && this.historyFromDate > this.historyToDate) {
      this.historyError.set('Başlangıç tarihi bitiş tarihinden sonra olamaz.');
      return false;
    }
    return true;
  }

  private loadHistory(type: CoachingInstitutionHistoryType, pageNumber: number): void {
    const requestId = ++this.historyRequestId;
    this.isHistoryLoading.set(true);
    this.historyError.set(null);
    this.history.set([]);
    this.institutionService.getStudentHistory(this.student.userId, type, { ...this.historyFilter(), pageNumber }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        if (requestId === this.historyRequestId) this.isHistoryLoading.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestId !== this.historyRequestId) return;
        this.history.set(page.items);
        this.historyPage.set(pageNumber);
        this.historyTotalCount.set(page.totalCount);
        this.historyTotalPages.set(Math.max(1, Math.ceil(page.totalCount / 25)));
      },
      error: () => {
        if (requestId === this.historyRequestId) {
          this.historyError.set(`${this.historyLabels[type]} geçmişi yüklenemedi. Öğrencinin kurum kaydını ve bağlantınızı kontrol edip tekrar deneyin.`);
        }
      }
    });
  }
}
