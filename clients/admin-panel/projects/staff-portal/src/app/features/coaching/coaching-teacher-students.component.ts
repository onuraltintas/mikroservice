import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CoachingTeacherStudent, CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherStudentDetailComponent } from './coaching-teacher-student-detail.component';

@Component({
  selector: 'staff-coaching-teacher-students',
  standalone: true,
  imports: [CommonModule, FormsModule, CoachingTeacherStudentDetailComponent],
  templateUrl: './coaching-teacher-students.component.html',
  styleUrl: './coaching-teacher-students.component.scss'
})
export class CoachingTeacherStudentsComponent implements OnInit {
  private readonly studentsService = inject(CoachingTeacherStudentsService);
  private requestVersion = 0;

  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly searchTerm = signal('');
  readonly selectedStudent = signal<CoachingTeacherStudent | null>(null);
  searchInput = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const requestVersion = ++this.requestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.studentsService.getMyStudents(this.pageNumber(), 25, this.searchTerm() || undefined).subscribe({
      next: page => {
        if (requestVersion !== this.requestVersion) return;
        this.students.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(Math.max(1, page.totalPages ?? Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => {
        if (requestVersion !== this.requestVersion) return;
        this.errorMessage.set('Öğrenci listesi yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.requestVersion) this.isLoading.set(false);
      }
    });
  }

  setSearchTerm(value: string): void {
    const search = value.trim();
    if (search === this.searchTerm()) return;
    this.searchTerm.set(search);
    this.pageNumber.set(1);
    this.load();
  }

  clearSearch(): void {
    this.searchInput = '';
    this.setSearchTerm('');
  }

  openReport(student: CoachingTeacherStudent): void {
    this.selectedStudent.set(student);
  }

  backToStudents(): void {
    this.selectedStudent.set(null);
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

  formatAssignmentDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    }).format(date);
  }

  trackById(_: number, student: CoachingTeacherStudent): string {
    return student.userId;
  }
}
