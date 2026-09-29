import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SpeedReadingTeacherClassOverview,
  SpeedReadingTeacherRosterFilters,
  SpeedReadingTeacherStudent,
  SpeedReadingTeacherService
} from './speed-reading-teacher.service';

type StudentStatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'staff-speed-reading-teacher-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './speed-reading-teacher-workspace.component.html',
  styleUrl: './speed-reading-teacher-workspace.component.scss'
})
export class SpeedReadingTeacherWorkspaceComponent implements OnInit {
  private readonly service = inject(SpeedReadingTeacherService);
  private rosterRequestVersion = 0;
  private overviewRequestVersion = 0;

  readonly students = signal<SpeedReadingTeacherStudent[]>([]);
  readonly overview = signal<SpeedReadingTeacherClassOverview | null>(null);
  readonly isLoading = signal(true);
  readonly isOverviewLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly overviewErrorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly filters = signal<SpeedReadingTeacherRosterFilters>({});

  searchInput = '';
  selectedGradeLevel = '';
  selectedStatus: StudentStatusFilter = 'all';
  readonly gradeLevels = Array.from({ length: 12 }, (_, index) => index + 1);

  ngOnInit(): void {
    this.loadStudents();
    this.loadOverview();
  }

  loadStudents(): void {
    const requestVersion = ++this.rosterRequestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.service.getMyStudents(this.pageNumber(), 25, this.filters()).subscribe({
      next: page => {
        if (requestVersion !== this.rosterRequestVersion) return;
        this.students.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => {
        if (requestVersion !== this.rosterRequestVersion) return;
        this.errorMessage.set('Öğrenci listesi yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.rosterRequestVersion) this.isLoading.set(false);
      }
    });
  }

  loadOverview(): void {
    const requestVersion = ++this.overviewRequestVersion;
    const dateTo = new Date();
    const dateFrom = new Date(dateTo);
    dateFrom.setUTCDate(dateFrom.getUTCDate() - 29);
    dateFrom.setUTCHours(0, 0, 0, 0);
    this.isOverviewLoading.set(true);
    this.overviewErrorMessage.set(null);
    this.service.getClassOverview(dateFrom, dateTo).subscribe({
      next: overview => {
        if (requestVersion === this.overviewRequestVersion) this.overview.set(overview);
      },
      error: () => {
        if (requestVersion !== this.overviewRequestVersion) return;
        this.overviewErrorMessage.set('Sınıf özeti yüklenemedi. Lütfen tekrar deneyin.');
        this.isOverviewLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.overviewRequestVersion) this.isOverviewLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    const filters: SpeedReadingTeacherRosterFilters = {};
    const searchTerm = this.searchInput.trim();
    const gradeLevel = Number(this.selectedGradeLevel);
    if (searchTerm) filters.searchTerm = searchTerm;
    if (Number.isInteger(gradeLevel) && gradeLevel >= 1 && gradeLevel <= 12) filters.gradeLevel = gradeLevel;
    if (this.selectedStatus !== 'all') filters.isActive = this.selectedStatus === 'active';
    this.filters.set(filters);
    this.pageNumber.set(1);
    this.loadStudents();
  }

  clearFilters(): void {
    this.searchInput = '';
    this.selectedGradeLevel = '';
    this.selectedStatus = 'all';
    this.filters.set({});
    this.pageNumber.set(1);
    this.loadStudents();
  }

  previousPage(): void {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.loadStudents();
  }

  nextPage(): void {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.loadStudents();
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 }).format(value);
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' }).format(date);
  }

  trackById(_: number, student: SpeedReadingTeacherStudent): string {
    return student.id;
  }
}
