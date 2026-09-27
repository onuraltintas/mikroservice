import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherAssignment } from '../../../core/services/coaching-portal.service';

type AssignmentStatusFilter = 'all' | 'Active' | 'Completed' | 'Cancelled';

@Component({
  selector: 'app-teacher-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './teacher-assignments.component.html',
  styleUrl: './teacher-assignments.component.scss'
})
export class TeacherAssignmentsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly assignments = signal<TeacherAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly statusFilter = signal<AssignmentStatusFilter>('all');
  readonly cancelConfirmationId = signal<string | null>(null);
  readonly isCancelling = signal(false);
  readonly successMessage = signal<string | null>(null);

  ngOnInit() {
    this.load();
  }

  load() {
    const teacherId = this.authService.userProfile()?.id;
    if (!teacherId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen profili bulunamadı.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    const status = this.statusFilter();
    const assignmentsRequest = status === 'all'
      ? this.coachingService.getTeacherAssignments(teacherId, this.pageNumber(), 25)
      : this.coachingService.getTeacherAssignments(teacherId, this.pageNumber(), 25, status);
    assignmentsRequest.pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: page => {
        this.assignments.set(page.items);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Ödevler yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  setStatusFilter(status: AssignmentStatusFilter) {
    this.statusFilter.set(status);
    this.pageNumber.set(1);
    this.cancelConfirmationId.set(null);
    this.load();
  }

  requestCancel(assignmentId: string) {
    this.errorMessage.set(null);
    this.cancelConfirmationId.set(assignmentId);
  }

  dismissCancel() {
    if (!this.isCancelling()) this.cancelConfirmationId.set(null);
  }

  cancelPendingAssignment() {
    const assignmentId = this.cancelConfirmationId();
    if (!assignmentId || this.isCancelling()) return;

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isCancelling.set(true);
    this.coachingService.cancelTeacherAssignment(assignmentId).pipe(
      finalize(() => this.isCancelling.set(false))
    ).subscribe({
      next: () => {
        this.cancelConfirmationId.set(null);
        this.successMessage.set('Ödev iptal edildi.');
        if (this.assignments().length === 1 && this.pageNumber() > 1) {
          this.pageNumber.update(page => page - 1);
        }
        this.load();
      },
      error: () => this.errorMessage.set('Ödev iptal edilemedi. Lütfen tekrar deneyin.')
    });
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

  trackById(_: number, item: TeacherAssignment) {
    return item.id;
  }

  assignmentStatusLabel(status: string) {
    switch (status) {
      case 'Active': return 'Aktif';
      case 'Completed': return 'Tamamlandı';
      case 'Cancelled': return 'İptal edildi';
      default: return status;
    }
  }
}
