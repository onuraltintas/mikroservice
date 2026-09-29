import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
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
  imports: [CommonModule, FormsModule, CoachingInstitutionStudentReportComponent, CoachingInstitutionTeacherReportComponent],
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
  readonly isInvitationSending = signal(false);
  readonly teacherInvitationError = signal<string | null>(null);
  readonly teacherInvitationSuccess = signal<string | null>(null);
  readonly studentInvitationError = signal<string | null>(null);
  readonly studentInvitationSuccess = signal<string | null>(null);
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
  teacherInviteEmail = '';
  studentInviteEmail = '';
  studentInviteTeacherUserId = '';

  ngOnInit(): void {
    this.loadScope();
  }

  selectSection(section: InstitutionSection): void {
    this.activeSection.set(section);
    this.selectedStudent.set(null);
    this.selectedTeacher.set(null);
    this.teacherInvitationError.set(null);
    this.studentInvitationError.set(null);
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

  sendTeacherInvitation(): void {
    const email = this.teacherInviteEmail.trim();
    if (!email || this.isInvitationSending()) return;
    this.isInvitationSending.set(true);
    this.teacherInvitationError.set(null);
    this.teacherInvitationSuccess.set(null);
    this.institutionService.inviteTeacher(email).subscribe({
      next: () => {
        if (this.teacherInviteEmail.trim() === email) this.teacherInviteEmail = '';
        this.teacherInvitationSuccess.set('Öğretmen daveti gönderildi. Kabul edildiğinde öğretmen kurum listenizde görünür.');
      },
      error: error => {
        this.teacherInvitationError.set(this.getInvitationError(error, 'Öğretmen daveti gönderilemedi. E-posta adresini ve kurum yetkinizi kontrol edip yeniden deneyin.'));
        this.isInvitationSending.set(false);
      },
      complete: () => this.isInvitationSending.set(false)
    });
  }

  sendStudentInvitation(): void {
    const email = this.studentInviteEmail.trim();
    if (!email || this.isInvitationSending()) return;
    const teacherUserId = this.studentInviteTeacherUserId || undefined;
    this.isInvitationSending.set(true);
    this.studentInvitationError.set(null);
    this.studentInvitationSuccess.set(null);
    this.institutionService.inviteStudent(email, teacherUserId).subscribe({
      next: () => {
        if (this.studentInviteEmail.trim() === email) {
          this.studentInviteEmail = '';
          this.studentInviteTeacherUserId = '';
        }
        this.studentInvitationSuccess.set('Öğrenci daveti gönderildi. Kabul edildiğinde öğrenci kurum listenizde görünür.');
      },
      error: error => {
        this.studentInvitationError.set(this.getInvitationError(error, 'Öğrenci daveti gönderilemedi. E-posta adresini ve kurum yetkinizi kontrol edip yeniden deneyin.'));
        this.isInvitationSending.set(false);
      },
      complete: () => this.isInvitationSending.set(false)
    });
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

  private getInvitationError(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse) || !error.error || typeof error.error !== 'object') return fallback;
    const body = error.error as Record<string, unknown>;
    const candidates = [body, body['error'], body['Error']];
    for (const candidate of candidates) {
      if (!candidate || typeof candidate !== 'object') continue;
      const payload = candidate as Record<string, unknown>;
      for (const key of ['message', 'description', 'Message', 'Description']) {
        if (typeof payload[key] === 'string') return payload[key] as string;
      }
    }
    return fallback;
  }
}
