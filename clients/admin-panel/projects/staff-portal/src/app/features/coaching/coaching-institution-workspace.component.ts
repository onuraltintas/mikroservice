import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import {
  CoachingInstitutionOverview,
  CoachingInstitutionService,
  CoachingInstitutionStudent,
  CoachingInstitutionTeacher
} from './coaching-institution.service';
import { CoachingInstitutionStudentReportComponent } from './coaching-institution-student-report.component';
import { CoachingInstitutionTeacherReportComponent } from './coaching-institution-teacher-report.component';

type InstitutionSection = 'overview' | 'students' | 'teachers';

@Component({
  selector: 'staff-coaching-institution-workspace',
  standalone: true,
  imports: [CommonModule, CoachingInstitutionStudentReportComponent, CoachingInstitutionTeacherReportComponent],
  templateUrl: './coaching-institution-workspace.component.html',
  styleUrl: './coaching-institution-workspace.component.scss'
})
export class CoachingInstitutionWorkspaceComponent implements OnInit {
  private readonly institutionService = inject(CoachingInstitutionService);
  private studentRequestId = 0;
  private teacherRequestId = 0;
  private filterTeacherRequestId = 0;

  readonly institutionId = signal<string | null>(null);
  readonly overview = signal<CoachingInstitutionOverview | null>(null);
  readonly students = signal<CoachingInstitutionStudent[]>([]);
  readonly teachers = signal<CoachingInstitutionTeacher[]>([]);
  readonly teacherFilterOptions = signal<CoachingInstitutionTeacher[]>([]);
  readonly selectedStudent = signal<CoachingInstitutionStudent | null>(null);
  readonly selectedTeacher = signal<CoachingInstitutionTeacher | null>(null);
  readonly activeSection = signal<InstitutionSection>('overview');
  readonly isLoadingScope = signal(true);
  readonly isLoadingOverview = signal(false);
  readonly isLoadingStudents = signal(false);
  readonly isLoadingTeachers = signal(false);
  readonly isLoadingTeacherFilter = signal(false);
  readonly loadingMoreStudents = signal(false);
  readonly loadingMoreTeachers = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly studentSearchTerm = signal('');
  readonly studentTeacherFilter = signal('');
  readonly studentGradeFilter = signal<number | null>(null);
  readonly teacherSearchTerm = signal('');
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentTotalCount = signal(0);
  readonly teacherPageNumber = signal(1);
  readonly teacherTotalPages = signal(1);
  readonly teacherTotalCount = signal(0);
  readonly teacherFilterPageNumber = signal(1);
  readonly teacherFilterTotalPages = signal(1);

  ngOnInit(): void {
    this.loadScope();
  }

  selectSection(section: InstitutionSection): void {
    this.activeSection.set(section);
    this.selectedStudent.set(null);
    this.selectedTeacher.set(null);
    if (section === 'students' && this.studentPageNumber() === 1 && this.students().length === 0) {
      this.loadStudents();
      this.loadTeacherFilterOptions();
    } else if (section === 'teachers' && this.teacherPageNumber() === 1 && this.teachers().length === 0) {
      this.loadTeachers();
    }
  }

  openStudentReport(student: CoachingInstitutionStudent): void {
    this.selectedStudent.set(student);
  }

  closeStudentReport(): void {
    this.selectedStudent.set(null);
  }

  openTeacherReport(teacher: CoachingInstitutionTeacher): void {
    this.selectedTeacher.set(teacher);
  }

  closeTeacherReport(): void {
    this.selectedTeacher.set(null);
  }

  loadScope(): void {
    this.isLoadingScope.set(true);
    this.errorMessage.set(null);
    this.institutionService.getReadScope().pipe(finalize(() => this.isLoadingScope.set(false))).subscribe({
      next: scope => {
        if (scope.isGlobal || !scope.institutionId) {
          this.errorMessage.set('Bu hesap için aktif kurum kapsamı doğrulanamadı. Lütfen kurum yöneticisi hesabıyla giriş yapın.');
          return;
        }
        this.institutionId.set(scope.institutionId);
        this.loadOverview();
      },
      error: () => this.errorMessage.set('Kurum yetkiniz doğrulanamadı. Lütfen oturumunuzu yenileyip tekrar deneyin.')
    });
  }

  loadOverview(): void {
    if (!this.institutionId()) return;
    this.isLoadingOverview.set(true);
    this.errorMessage.set(null);
    this.institutionService.getOverview(10).pipe(finalize(() => this.isLoadingOverview.set(false))).subscribe({
      next: overview => this.overview.set(overview),
      error: () => this.errorMessage.set('Kurum özeti yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  loadStudents(append = false): void {
    const institutionId = this.institutionId();
    if (!institutionId) return;
    const requestId = ++this.studentRequestId;
    const pageNumber = append ? this.studentPageNumber() + 1 : 1;
    if (!append) this.loadingMoreStudents.set(false);
    const loading = append ? this.loadingMoreStudents : this.isLoadingStudents;
    loading.set(true);
    this.errorMessage.set(null);
    this.institutionService.getStudentRoster(
      institutionId,
      pageNumber,
      this.studentSearchTerm(),
      this.studentTeacherFilter() || undefined,
      this.studentGradeFilter()
    ).pipe(finalize(() => {
      if (requestId === this.studentRequestId) loading.set(false);
    })).subscribe({
      next: page => {
        if (requestId !== this.studentRequestId) return;
        this.students.update(current => append ? [...current, ...page.students] : page.students);
        this.studentPageNumber.set(pageNumber);
        this.studentTotalCount.set(page.totalCount);
        this.studentTotalPages.set(Math.max(1, Math.ceil(page.totalCount / 25)));
      },
      error: () => {
        if (requestId === this.studentRequestId) this.errorMessage.set('Kurum öğrenci listesi yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  searchStudents(value: string): void {
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  setTeacherFilter(value: string): void {
    this.studentTeacherFilter.set(value);
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  setGradeFilter(value: string): void {
    const grade = value ? Number(value) : null;
    this.studentGradeFilter.set(grade !== null && Number.isInteger(grade) && grade >= 1 && grade <= 12 ? grade : null);
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  loadMoreStudents(): void {
    if (!this.isLoadingStudents() && !this.loadingMoreStudents()
      && this.studentPageNumber() < this.studentTotalPages()) this.loadStudents(true);
  }

  loadTeachers(append = false): void {
    const institutionId = this.institutionId();
    if (!institutionId) return;
    const requestId = ++this.teacherRequestId;
    const pageNumber = append ? this.teacherPageNumber() + 1 : 1;
    if (!append) this.loadingMoreTeachers.set(false);
    const loading = append ? this.loadingMoreTeachers : this.isLoadingTeachers;
    loading.set(true);
    this.errorMessage.set(null);
    this.institutionService.getTeacherRoster(institutionId, pageNumber, this.teacherSearchTerm()).pipe(
      finalize(() => {
        if (requestId === this.teacherRequestId) loading.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestId !== this.teacherRequestId) return;
        this.teachers.update(current => append ? [...current, ...page.teachers] : page.teachers);
        this.teacherPageNumber.set(pageNumber);
        this.teacherTotalCount.set(page.totalCount);
        this.teacherTotalPages.set(Math.max(1, Math.ceil(page.totalCount / 25)));
      },
      error: () => {
        if (requestId === this.teacherRequestId) this.errorMessage.set('Kurum öğretmen listesi yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  searchTeachers(value: string): void {
    this.teacherSearchTerm.set(value.trim());
    this.teacherPageNumber.set(1);
    this.loadTeachers();
  }

  loadMoreTeachers(): void {
    if (!this.isLoadingTeachers() && !this.loadingMoreTeachers()
      && this.teacherPageNumber() < this.teacherTotalPages()) this.loadTeachers(true);
  }

  loadMoreTeacherFilterOptions(): void {
    if (!this.isLoadingTeacherFilter() && this.teacherFilterPageNumber() < this.teacherFilterTotalPages()) {
      this.loadTeacherFilterOptions(true);
    }
  }

  retry(): void {
    if (!this.institutionId()) this.loadScope();
    else if (this.activeSection() === 'students') this.loadStudents();
    else if (this.activeSection() === 'teachers') this.loadTeachers();
    else this.loadOverview();
  }

  trackByUserId(_: number, user: CoachingInstitutionStudent | CoachingInstitutionTeacher): string {
    return user.userId;
  }

  private loadTeacherFilterOptions(append = false): void {
    const institutionId = this.institutionId();
    if (!institutionId) return;
    const requestId = ++this.filterTeacherRequestId;
    const pageNumber = append ? this.teacherFilterPageNumber() + 1 : 1;
    this.isLoadingTeacherFilter.set(true);
    this.institutionService.getTeacherRoster(institutionId, pageNumber, '', 100).pipe(
      finalize(() => {
        if (requestId === this.filterTeacherRequestId) this.isLoadingTeacherFilter.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestId !== this.filterTeacherRequestId) return;
        this.teacherFilterOptions.update(current => [...new Map([...current, ...page.teachers].map(teacher => [teacher.userId, teacher])).values()]);
        this.teacherFilterPageNumber.set(pageNumber);
        this.teacherFilterTotalPages.set(Math.max(1, Math.ceil(page.totalCount / 100)));
      },
      error: () => {
        if (requestId === this.filterTeacherRequestId) this.errorMessage.set('Öğretmen filtresi yüklenemedi. Öğrencileri aramaya devam edebilirsiniz.');
      }
    });
  }
}
