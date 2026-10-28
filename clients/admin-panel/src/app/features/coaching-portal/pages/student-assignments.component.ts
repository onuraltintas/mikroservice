import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, StudentAssignment, StudentAssignmentFilter } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-student-assignments',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './student-assignments.component.html',
  styleUrl: './student-assignments.component.scss'
})
export class StudentAssignmentsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly assignments = signal<StudentAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  // Roadmap O1: the list opens on work still waiting (late first) and every filter runs on the server.
  readonly filter = signal<StudentAssignmentFilter>('open');
  readonly filters: { label: string; value: StudentAssignmentFilter }[] = [
    { label: 'Yapılacaklar', value: 'open' },
    { label: 'Geciken', value: 'overdue' },
    { label: 'Bekleyen', value: 'pending' },
    { label: 'Teslim ettiklerim', value: 'submitted' },
    { label: 'İptal edilen', value: 'cancelled' },
    { label: 'Tümü', value: 'all' }
  ];
  readonly emptyMessage: Record<StudentAssignmentFilter, string> = {
    open: 'Yapılacak ödevin yok. Yeni ödev gelince burada görünür.',
    overdue: 'Geciken ödevin yok.',
    pending: 'Son tarihi gelmemiş bekleyen ödevin yok.',
    submitted: 'Henüz teslim ettiğin ödev yok.',
    cancelled: 'İptal edilen ödev yok.',
    all: 'Henüz sana verilmiş bir ödev yok.'
  };

  ngOnInit() {
    this.load();
  }

  load() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğrenci profili bulunamadı.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.coachingService.getStudentAssignments(studentId, this.pageNumber(), 25, this.filter()).subscribe({
      next: page => {
        this.assignments.set(page.items);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Ödevler yüklenemedi. Bağlantını kontrol edip yeniden dene.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  assignmentStatusLabel(assignment: StudentAssignment) {
    if (assignment.assignmentStatus === 'Cancelled') return 'İptal edildi';
    if (assignment.assignmentStatus === 'Completed') return 'Tamamlandı';
    if (assignment.status === 'Graded') return 'Değerlendirildi';
    if (assignment.submittedAt || assignment.status === 'Submitted') return 'Teslim edildi';
    return assignment.isOverdue ? 'Gecikti' : 'Çalışmanı bekliyor';
  }

  setFilter(value: StudentAssignmentFilter) {
    this.filter.set(value);
    this.pageNumber.set(1);
    this.load();
  }

  previousPage() {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.load();
  }

  nextPage() {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.load();
  }

  trackById(_: number, item: StudentAssignment) {
    return item.id;
  }
  private readonly feedback = coachingFeedback(undefined, this.errorMessage);
}
