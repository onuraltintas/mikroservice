import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { CoachingPortalService, TeacherStudent } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-students',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './teacher-students.component.html',
  styleUrl: './teacher-students.component.scss'
})
export class TeacherStudentsComponent implements OnInit {
  private readonly coachingService = inject(CoachingPortalService);

  readonly students = signal<TeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly searchTerm = signal('');

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.coachingService.getTeacherStudents(this.pageNumber(), 25, this.searchTerm() || undefined).subscribe({
      next: page => {
        this.students.set(page.items);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Öğrenci listesi yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  setSearchTerm(value: string) {
    this.searchTerm.set(value.trim());
    this.pageNumber.set(1);
    this.load();
  }

  clearSearch() {
    if (!this.searchTerm()) return;
    this.setSearchTerm('');
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

  trackById(_: number, student: TeacherStudent) {
    return student.userId;
  }
}
