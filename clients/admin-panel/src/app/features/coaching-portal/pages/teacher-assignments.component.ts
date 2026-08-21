import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherAssignment } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-assignments',
  standalone: true,
  imports: [CommonModule, RouterLink],
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
    this.coachingService.getTeacherAssignments(teacherId, this.pageNumber(), 25).subscribe({
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
}
