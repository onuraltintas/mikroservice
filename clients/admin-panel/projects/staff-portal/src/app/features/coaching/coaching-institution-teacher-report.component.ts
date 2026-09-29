import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import {
  CoachingInstitutionService,
  CoachingInstitutionStudent,
  CoachingInstitutionTeacher,
  CoachingInstitutionTeacherAnalytics,
  CoachingInstitutionTeacherOverview
} from './coaching-institution.service';

@Component({
  selector: 'staff-coaching-institution-teacher-report',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './coaching-institution-teacher-report.component.html',
  styleUrl: './coaching-institution-teacher-report.component.scss'
})
export class CoachingInstitutionTeacherReportComponent implements OnInit {
  private readonly institutionService = inject(CoachingInstitutionService);
  private readonly destroyRef = inject(DestroyRef);
  private overviewRequestId = 0;
  private analyticsRequestId = 0;
  private studentsRequestId = 0;

  @Input({ required: true }) teacher!: CoachingInstitutionTeacher;
  @Input({ required: true }) institutionId!: string;
  @Output() readonly back = new EventEmitter<void>();
  @Output() readonly studentSelected = new EventEmitter<CoachingInstitutionStudent>();

  readonly overview = signal<CoachingInstitutionTeacherOverview | null>(null);
  readonly analytics = signal<CoachingInstitutionTeacherAnalytics | null>(null);
  readonly students = signal<CoachingInstitutionStudent[]>([]);
  readonly studentSearch = signal('');
  readonly gradeFilter = signal<number | null>(null);
  readonly studentPage = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentTotalCount = signal(0);
  readonly isOverviewLoading = signal(true);
  readonly isAnalyticsLoading = signal(true);
  readonly isStudentsLoading = signal(false);
  readonly isLoadingMoreStudents = signal(false);
  readonly overviewError = signal<string | null>(null);
  readonly analyticsError = signal<string | null>(null);
  readonly studentsError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadOverview();
    this.loadAnalytics();
    this.loadStudents();
  }

  loadOverview(): void {
    if (!this.teacher?.userId) return;
    const requestId = ++this.overviewRequestId;
    this.isOverviewLoading.set(true);
    this.overviewError.set(null);
    this.institutionService.getTeacherOverview(this.teacher.userId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { if (requestId === this.overviewRequestId) this.isOverviewLoading.set(false); })
    ).subscribe({
      next: overview => { if (requestId === this.overviewRequestId) this.overview.set(overview); },
      error: () => {
        if (requestId === this.overviewRequestId) this.overviewError.set('Öğretmen özeti yüklenemedi. Kurum yetkinizi ve bağlantınızı kontrol edip tekrar deneyin.');
      }
    });
  }

  loadAnalytics(): void {
    if (!this.teacher?.userId) return;
    const requestId = ++this.analyticsRequestId;
    this.isAnalyticsLoading.set(true);
    this.analyticsError.set(null);
    this.institutionService.getTeacherAnalytics(this.teacher.userId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { if (requestId === this.analyticsRequestId) this.isAnalyticsLoading.set(false); })
    ).subscribe({
      next: analytics => { if (requestId === this.analyticsRequestId) this.analytics.set(analytics); },
      error: () => {
        if (requestId === this.analyticsRequestId) this.analyticsError.set('Öğretmen dönem analizi yüklenemedi. Kurum yetkinizi ve bağlantınızı kontrol edip tekrar deneyin.');
      }
    });
  }

  loadStudents(append = false): void {
    if (!this.institutionId || !this.teacher?.userId) return;
    const requestId = ++this.studentsRequestId;
    const pageNumber = append ? this.studentPage() + 1 : 1;
    const loading = append ? this.isLoadingMoreStudents : this.isStudentsLoading;
    if (!append) this.isLoadingMoreStudents.set(false);
    loading.set(true);
    this.studentsError.set(null);
    this.institutionService.getStudentRoster(
      this.institutionId, pageNumber, this.studentSearch(), this.teacher.userId, this.gradeFilter()
    ).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { if (requestId === this.studentsRequestId) loading.set(false); })
    ).subscribe({
      next: page => {
        if (requestId !== this.studentsRequestId) return;
        this.students.update(current => append ? [...current, ...page.students] : page.students);
        this.studentPage.set(pageNumber);
        this.studentTotalCount.set(page.totalCount);
        this.studentTotalPages.set(Math.max(1, Math.ceil(page.totalCount / 25)));
      },
      error: () => {
        if (requestId === this.studentsRequestId) this.studentsError.set('Öğretmene bağlı kurum öğrencileri yüklenemedi. Tekrar deneyin.');
      }
    });
  }

  searchStudents(value: string): void {
    this.studentSearch.set(value.trim());
    this.loadStudents();
  }

  setGradeFilter(value: string): void {
    const grade = value ? Number(value) : null;
    this.gradeFilter.set(grade !== null && Number.isInteger(grade) && grade >= 1 && grade <= 12 ? grade : null);
    this.loadStudents();
  }

  nextStudentsPage(): void {
    if (!this.isStudentsLoading() && !this.isLoadingMoreStudents()
      && this.studentPage() < this.studentTotalPages()) this.loadStudents(true);
  }

  retryOverview(): void { this.loadOverview(); }
  retryAnalytics(): void { this.loadAnalytics(); }
  retryStudents(): void { this.loadStudents(); }

  openStudentReport(student: CoachingInstitutionStudent): void {
    this.studentSelected.emit(student);
  }
}
