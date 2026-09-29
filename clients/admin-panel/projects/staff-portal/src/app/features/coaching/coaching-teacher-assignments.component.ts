import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import {
  CoachingAssignmentStatus,
  CoachingTeacherAssignment,
  CoachingTeacherAssignmentsService
} from './coaching-teacher-assignments.service';
import { CoachingTeacherAssignmentFormComponent } from './coaching-teacher-assignment-form.component';
import { CoachingTeacherAssignmentDetailComponent } from './coaching-teacher-assignment-detail.component';

type AssignmentStatusFilter = 'all' | CoachingAssignmentStatus;
type AssignmentFormMode = { kind: 'new' } | { kind: 'edit'; id: string };

@Component({
  selector: 'staff-coaching-teacher-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule, CoachingTeacherAssignmentFormComponent, CoachingTeacherAssignmentDetailComponent],
  templateUrl: './coaching-teacher-assignments.component.html',
  styleUrl: './coaching-teacher-assignments.component.scss'
})
export class CoachingTeacherAssignmentsComponent implements OnInit {
  private readonly auth = inject(StaffAuthService);
  private readonly assignmentsService = inject(CoachingTeacherAssignmentsService);
  private requestVersion = 0;

  readonly assignments = signal<CoachingTeacherAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly statusFilter = signal<AssignmentStatusFilter>('all');
  readonly cancelConfirmationId = signal<string | null>(null);
  readonly isCancelling = signal(false);
  readonly formMode = signal<AssignmentFormMode | null>(null);
  readonly detailAssignmentId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  createAssignment(): void {
    this.successMessage.set(null);
    this.detailAssignmentId.set(null);
    this.formMode.set({ kind: 'new' });
  }

  editAssignment(assignmentId: string): void {
    this.successMessage.set(null);
    this.detailAssignmentId.set(null);
    this.formMode.set({ kind: 'edit', id: assignmentId });
  }

  reviewAssignment(assignmentId: string): void {
    this.successMessage.set(null);
    this.formMode.set(null);
    this.detailAssignmentId.set(assignmentId);
  }

  closeAssignmentDetail(): void {
    this.detailAssignmentId.set(null);
  }

  closeEditor(): void {
    this.formMode.set(null);
  }

  onFormSaved(result: 'created' | 'updated'): void {
    this.formMode.set(null);
    this.successMessage.set(result === 'created' ? 'Ödev oluşturuldu.' : 'Ödev güncellendi.');
    this.load();
  }

  load(): void {
    const teacherId = this.auth.getCurrentUserId();
    if (!teacherId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu doğrulanamadı. Lütfen yeniden giriş yapın.');
      return;
    }

    const requestVersion = ++this.requestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const status = this.statusFilter();
    this.assignmentsService.getTeacherAssignments(
      teacherId,
      this.pageNumber(),
      25,
      status === 'all' ? undefined : status
    ).pipe(finalize(() => {
      if (requestVersion === this.requestVersion) this.isLoading.set(false);
    })).subscribe({
      next: page => {
        if (requestVersion !== this.requestVersion) return;
        this.assignments.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(Math.max(1, page.totalPages ?? Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => {
        if (requestVersion === this.requestVersion) {
          this.errorMessage.set('Ödev listesi yüklenemedi. Lütfen tekrar deneyin.');
        }
      }
    });
  }

  setStatusFilter(status: AssignmentStatusFilter): void {
    if (status === this.statusFilter()) return;
    this.statusFilter.set(status);
    this.pageNumber.set(1);
    this.cancelConfirmationId.set(null);
    this.successMessage.set(null);
    this.load();
  }

  requestCancel(assignmentId: string): void {
    this.errorMessage.set(null);
    this.cancelConfirmationId.set(assignmentId);
  }

  dismissCancel(): void {
    if (!this.isCancelling()) this.cancelConfirmationId.set(null);
  }

  cancelPendingAssignment(): void {
    const assignmentId = this.cancelConfirmationId();
    if (!assignmentId || this.isCancelling()) return;

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isCancelling.set(true);
    this.assignmentsService.cancelAssignment(assignmentId).pipe(
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

  previousPage(): void {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.load();
  }

  nextPage(): void {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.load();
  }

  trackById(_: number, assignment: CoachingTeacherAssignment): string {
    return assignment.id;
  }

  assignmentStatusLabel(status: string): string {
    switch (status) {
      case 'Active': return 'Aktif';
      case 'Completed': return 'Tamamlandı';
      case 'Cancelled': return 'İptal edildi';
      default: return status;
    }
  }
}
