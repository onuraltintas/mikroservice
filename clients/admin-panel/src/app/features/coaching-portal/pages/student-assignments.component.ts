import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, StudentAssignment } from '../../../core/services/coaching-portal.service';

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
  readonly filter = signal<'all' | 'pending' | 'submitted' | 'overdue'>('all');
  readonly filters = [
    { label: 'Tümü', value: 'all' as const },
    { label: 'Bekleyen', value: 'pending' as const },
    { label: 'Teslim', value: 'submitted' as const },
    { label: 'Geciken', value: 'overdue' as const }
  ];

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
    this.coachingService.getStudentAssignments(studentId, this.pageNumber(), 25).subscribe({
      next: page => {
        this.assignments.set(page.items);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Ödevler yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  visibleAssignments() {
    const currentFilter = this.filter();
    return this.assignments().filter(assignment => {
      if (currentFilter === 'overdue') return assignment.isOverdue;
      if (currentFilter === 'submitted') return !!assignment.submittedAt;
      if (currentFilter === 'pending') return !assignment.submittedAt;
      return true;
    });
  }

  setFilter(value: 'all' | 'pending' | 'submitted' | 'overdue') {
    this.filter.set(value);
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
}
