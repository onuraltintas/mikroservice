import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import {
  CoachingPortalService,
  CoachingStudentHistoryItem,
  CoachingStudentHistoryType,
  PagedResponse,
  StudentProgressSummary,
  TeacherStudent
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-student-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './teacher-student-detail.component.html'
})
export class TeacherStudentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly coachingService = inject(CoachingPortalService);

  readonly student = signal<TeacherStudent | null>(null);
  readonly progress = signal<StudentProgressSummary | null>(null);
  readonly history = signal<CoachingStudentHistoryItem[]>([]);
  readonly historyTypes: CoachingStudentHistoryType[] = ['Assignments', 'Exams', 'Sessions', 'Goals'];
  readonly historyType = signal<CoachingStudentHistoryType>('Assignments');
  readonly historyPage = signal(1);
  readonly historyTotalPages = signal(0);
  readonly isLoading = signal(true);
  readonly isHistoryLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly historyError = signal<string | null>(null);
  private historyRequestId = 0;

  ngOnInit() {
    const studentId = this.route.snapshot.paramMap.get('studentId');
    if (!studentId) {
      this.errorMessage.set('Öğrenci bilgisi bulunamadı.');
      this.isLoading.set(false);
      return;
    }

    this.coachingService.getTeacherStudents(1, 1, undefined, [studentId]).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: page => {
        const student = page.items.find(item => item.userId === studentId);
        if (!student) {
          this.errorMessage.set('Bu öğrenci artık aktif öğrenci listenizde değil.');
          return;
        }

        this.student.set(student);
        this.coachingService.getStudentProgress(studentId).subscribe({
          next: summary => this.progress.set(summary),
          error: () => this.errorMessage.set('Öğrenci ilerleme özeti yüklenemedi.')
        });
        this.loadHistory('Assignments', 1);
      },
      error: () => this.errorMessage.set('Öğrenci bilgisi yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  selectHistoryType(type: CoachingStudentHistoryType) {
    this.historyType.set(type);
    this.loadHistory(type, 1);
  }

  previousHistoryPage() {
    if (this.historyPage() > 1) this.loadHistory(this.historyType(), this.historyPage() - 1);
  }

  nextHistoryPage() {
    if (this.historyPage() < this.historyTotalPages()) {
      this.loadHistory(this.historyType(), this.historyPage() + 1);
    }
  }

  historyLabel(type: CoachingStudentHistoryType) {
    return ({ Assignments: 'Ödevler', Exams: 'Sınavlar', Sessions: 'Seanslar', Goals: 'Hedefler' })[type];
  }

  trackById(_: number, item: CoachingStudentHistoryItem) {
    return `${item.type}:${item.id}`;
  }

  private loadHistory(type: CoachingStudentHistoryType, pageNumber: number) {
    const studentId = this.student()?.userId;
    if (!studentId) return;
    const requestId = ++this.historyRequestId;

    this.isHistoryLoading.set(true);
    this.historyError.set(null);
    this.history.set([]);
    this.coachingService.getTeacherStudentHistory(studentId, type, pageNumber, 10).pipe(
      finalize(() => { if (requestId === this.historyRequestId) this.isHistoryLoading.set(false); })
    ).subscribe({
      next: (page: PagedResponse<CoachingStudentHistoryItem>) => {
        if (requestId !== this.historyRequestId) return;
        this.history.set(page.items);
        this.historyPage.set(page.pageNumber);
        this.historyTotalPages.set(page.totalPages ?? Math.ceil(page.totalCount / page.pageSize));
      },
      error: () => { if (requestId === this.historyRequestId) this.historyError.set(`${this.historyLabel(type)} geçmişi yüklenemedi.`); }
    });
  }
}
