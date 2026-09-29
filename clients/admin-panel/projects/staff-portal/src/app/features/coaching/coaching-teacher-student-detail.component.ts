import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import {
  coachingHistoryCsv,
  collectCoachingHistory,
  downloadCoachingHistoryCsv,
  renderCoachingHistoryPrint
} from '../../../../../../src/app/features/coaching-portal/coaching-history-export';
import {
  CoachingStudentHistoryFilter,
  CoachingStudentHistoryItem,
  CoachingStudentHistoryType,
  CoachingStudentProgressSummary,
  CoachingTeacherStudent,
  PagedCoachingResponse,
  CoachingTeacherStudentsService
} from './coaching-teacher-students.service';

@Component({
  selector: 'staff-coaching-teacher-student-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-student-detail.component.html',
  styleUrl: './coaching-teacher-student-detail.component.scss'
})
export class CoachingTeacherStudentDetailComponent implements OnInit {
  private readonly studentsService = inject(CoachingTeacherStudentsService);
  private readonly destroyRef = inject(DestroyRef);
  private progressRequestId = 0;
  private historyRequestId = 0;

  @Input({ required: true }) student!: CoachingTeacherStudent;
  @Output() readonly back = new EventEmitter<void>();

  readonly progress = signal<CoachingStudentProgressSummary | null>(null);
  readonly history = signal<CoachingStudentHistoryItem[]>([]);
  readonly historyTypes: CoachingStudentHistoryType[] = ['Assignments', 'Exams', 'Sessions', 'Goals'];
  readonly historyLabels: Record<CoachingStudentHistoryType, string> = {
    Assignments: 'Ödevler', Exams: 'Sınavlar', Sessions: 'Seanslar', Goals: 'Hedefler'
  };
  readonly statusOptions: Record<CoachingStudentHistoryType, { value: string; label: string }[]> = {
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
  readonly historyType = signal<CoachingStudentHistoryType>('Assignments');
  readonly historyPage = signal(1);
  readonly historyTotalPages = signal(1);
  readonly isProgressLoading = signal(true);
  readonly progressError = signal<string | null>(null);
  readonly isHistoryLoading = signal(false);
  readonly isExporting = signal(false);
  readonly historyError = signal<string | null>(null);
  historySearch = '';
  historyStatus = '';
  historyFromDate = '';
  historyToDate = '';

  ngOnInit(): void {
    this.loadProgress();
    this.loadHistory('Assignments', 1);
  }

  loadProgress(): void {
    const requestId = ++this.progressRequestId;
    this.isProgressLoading.set(true);
    this.progressError.set(null);
    this.studentsService.getStudentProgress(this.student.userId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        if (requestId === this.progressRequestId) this.isProgressLoading.set(false);
      })
    ).subscribe({
      next: summary => {
        if (requestId === this.progressRequestId) this.progress.set(summary);
      },
      error: error => {
        if (requestId === this.progressRequestId) {
          this.progressError.set(this.readableError(error, 'Öğrenci ilerleme özeti yüklenemedi.'));
        }
      }
    });
  }

  selectHistoryType(type: CoachingStudentHistoryType): void {
    this.historyType.set(type);
    this.historyStatus = '';
    this.loadHistory(type, 1);
  }

  applyFilters(): void {
    if (!this.validateHistoryRange()) return;
    this.loadHistory(this.historyType(), 1);
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

  async exportHistory(): Promise<void> {
    if (this.isExporting() || !this.validateHistoryRange()) return;
    const type = this.historyType();
    this.isExporting.set(true);
    this.historyError.set(null);
    try {
      const records = await collectCoachingHistory((page, pageSize) =>
        this.studentsService.getStudentHistory(this.student.userId, type, page, pageSize, this.historyFilter()));
      downloadCoachingHistoryCsv(coachingHistoryCsv(records), `kocluk-${this.student.userId}-${type.toLowerCase()}.csv`);
    } catch {
      this.historyError.set('Tam rapor indirilemedi. Kayıtlar değişmiş veya bağlantı kesilmiş olabilir; yeniden deneyin.');
    } finally {
      this.isExporting.set(false);
    }
  }

  async printHistory(): Promise<void> {
    if (this.isExporting() || !this.validateHistoryRange()) return;
    const target = window.open('', '_blank');
    if (!target) {
      this.historyError.set('Yazdırma penceresi açılamadı. Tarayıcınızın açılır pencere iznini kontrol edin.');
      return;
    }
    target.opener = null;
    const type = this.historyType();
    this.isExporting.set(true);
    this.historyError.set(null);
    try {
      const records = await collectCoachingHistory((page, pageSize) =>
        this.studentsService.getStudentHistory(this.student.userId, type, page, pageSize, this.historyFilter()));
      renderCoachingHistoryPrint(target, records, `${this.student.fullName} · ${this.historyLabels[type]} · Koçluk geçmişi`);
    } catch {
      target.close();
      this.historyError.set('Tam rapor yazdırılamadı. Kayıtlar değişmiş veya bağlantı kesilmiş olabilir; yeniden deneyin.');
    } finally {
      this.isExporting.set(false);
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

  trackById(_: number, item: CoachingStudentHistoryItem): string {
    return `${item.type}:${item.id}`;
  }

  private historyFilter(): CoachingStudentHistoryFilter {
    return {
      ...(this.historyFromDate ? { fromDate: `${this.historyFromDate}T00:00:00.000Z` } : {}),
      ...(this.historyToDate ? { toDate: `${this.historyToDate}T23:59:59.999Z` } : {}),
      ...(this.historyStatus ? { status: this.historyStatus } : {}),
      ...(this.historySearch.trim() ? { search: this.historySearch.trim() } : {})
    };
  }

  private validateHistoryRange(): boolean {
    if (this.historyFromDate && this.historyToDate && this.historyFromDate > this.historyToDate) {
      this.historyError.set('Başlangıç tarihi bitiş tarihinden sonra olamaz.');
      return false;
    }
    return true;
  }

  private loadHistory(type: CoachingStudentHistoryType, pageNumber: number): void {
    const requestId = ++this.historyRequestId;
    this.isHistoryLoading.set(true);
    this.historyError.set(null);
    this.history.set([]);
    this.studentsService.getStudentHistory(this.student.userId, type, pageNumber, 10, this.historyFilter()).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        if (requestId === this.historyRequestId) this.isHistoryLoading.set(false);
      })
    ).subscribe({
      next: (page: PagedCoachingResponse<CoachingStudentHistoryItem>) => {
        if (requestId !== this.historyRequestId) return;
        this.history.set(page.items);
        this.historyPage.set(page.pageNumber);
        this.historyTotalPages.set(Math.max(1, page.totalPages ?? Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: error => {
        if (requestId === this.historyRequestId) {
          this.historyError.set(this.readableError(error, `${this.historyLabels[type]} geçmişi yüklenemedi.`));
        }
      }
    });
  }

  private readableError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'Oturum süreniz sona erdi. Yeniden giriş yapıp tekrar deneyin.';
    }
    if (error instanceof HttpErrorResponse && error.status === 403) {
      return 'Bu öğrenci raporunu görüntüleme yetkiniz bulunmuyor. Öğrenci atamasını ve yetkinizi kontrol edin.';
    }
    return fallback;
  }
}
