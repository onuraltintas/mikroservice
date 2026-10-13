import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  CoachingInstitutionOverview,
  CoachingInstitutionService,
  CoachingInstitutionStudent,
  CoachingInstitutionTeacher
} from './coaching-institution.service';
import { CoachingInstitutionStudentReportComponent } from './coaching-institution-student-report.component';
import { CoachingInstitutionAnalyticsComponent } from './coaching-institution-analytics.component';
import { CoachingInstitutionSubscriptionComponent } from './coaching-institution-subscription.component';
import { CoachingInstitutionTeacherReportComponent } from './coaching-institution-teacher-report.component';
import { StaffPendingInvitationsComponent } from '../staff-pending-invitations.component';
import { CoachingFinanceComponent } from '../../../../../../src/app/features/coaching-portal/pages/coaching-finance.component';

type InstitutionSection = 'overview' | 'students' | 'teachers' | 'analytics' | 'subscription' | 'finance';

@Component({
  selector: 'staff-coaching-institution-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, CoachingInstitutionStudentReportComponent, CoachingInstitutionTeacherReportComponent, CoachingInstitutionAnalyticsComponent, CoachingInstitutionSubscriptionComponent, StaffPendingInvitationsComponent, CoachingFinanceComponent],
  templateUrl: './coaching-institution-workspace.component.html',
  styleUrl: './coaching-institution-workspace.component.scss'
})
export class CoachingInstitutionWorkspaceComponent implements OnInit {
  @ViewChild(CoachingInstitutionSubscriptionComponent) subscriptionPanel?: Pick<CoachingInstitutionSubscriptionComponent, 'canLeavePage'>;
  @ViewChild(CoachingInstitutionStudentReportComponent) studentReportPanel?: Pick<CoachingInstitutionStudentReportComponent, 'canLeavePage'>;
  @ViewChild(CoachingFinanceComponent) financePanel?: Pick<CoachingFinanceComponent, 'canLeavePage'>;
  private readonly institutionService = inject(CoachingInstitutionService);
  private readonly destroyRef = inject(DestroyRef);
  readonly editingStudentId = signal<string | null>(null);
  readonly isMembershipSaving = signal(false);
  readonly membershipError = signal<string | null>(null);
  readonly membershipSuccess = signal<string | null>(null);
  editStudentGrade: number | null = null;
  editStudentTeacherId = '';
  membershipDirty = false;

  canLeavePage(): boolean {
    if (!(this.studentReportPanel?.canLeavePage() ?? true) || !(this.financePanel?.canLeavePage() ?? true)) return false;
    if (!(this.subscriptionPanel?.canLeavePage() ?? true)) return false;
    if (this.isMembershipSaving()) { this.membershipError.set('Üye işlemi sürüyor. Tamamlanmasını bekleyin.'); return false; }
    return !this.membershipDirty || window.confirm('Kaydedilmemiş öğrenci değişikliklerinden vazgeçerek ayrılmak istiyor musunuz?');
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.isMembershipSaving() || this.membershipDirty) { event.preventDefault(); event.returnValue = ''; }
  }
  startStudentEdit(student: CoachingInstitutionStudent): void {
    if (!this.students().some(item => item.userId === student.userId) || !this.canLeavePage()) return;
    this.editingStudentId.set(student.userId); this.editStudentGrade = student.gradeLevel ?? null;
    this.editStudentTeacherId = student.teacherUserId ?? ''; this.membershipDirty = false; this.membershipError.set(null);
  }
  cancelStudentEdit(): void {
    this.editingStudentId.set(null); this.membershipDirty = false;
  }
  saveStudentEdit(): void {
    const id = this.editingStudentId(); const grade = this.editStudentGrade;
    if (!id || this.isMembershipSaving()) return;
    if (grade === null || !Number.isInteger(grade) || grade < 1 || grade > 12) {
      this.membershipError.set('1–12 arasında geçerli bir sınıf seçin.'); return;
    }
    const original = this.students().find(student => student.userId === id);
    if (!original) return;
    if (this.editStudentTeacherId && this.editStudentTeacherId !== original.teacherUserId
      && !this.teacherFilterOptions().some(teacher => teacher.userId === this.editStudentTeacherId)) {
      this.membershipError.set('Kurum öğretmenlerinden birini seçin.'); return;
    }
    this.changeMembership(this.institutionService.updateStudent(id, grade, this.editStudentTeacherId || null), 'Öğrenci sınıfı ve öğretmen ataması güncellendi.', () => {
      this.cancelStudentEdit(); this.loadStudents();
    });
  }
  removeStudent(student: CoachingInstitutionStudent): void {
    if (this.isMembershipSaving() || this.editingStudentId() || !this.students().some(item => item.userId === student.userId)) return;
    if (!window.confirm(`${student.firstName} ${student.lastName} kurumdan çıkarılsın mı? Kurum ve öğretmen erişimi sona erer; geçmiş koçluk kayıtları silinmez.`)) return;
    this.changeMembership(this.institutionService.removeStudent(student.userId), 'Öğrencinin kurum ilişkisi sona erdi.', () => this.loadStudents());
  }
  removeTeacher(teacher: CoachingInstitutionTeacher): void {
    if (this.isMembershipSaving() || this.editingStudentId() || !this.teachers().some(item => item.userId === teacher.userId)) return;
    if (!window.confirm(`${teacher.firstName} ${teacher.lastName} kurumdan çıkarılsın mı? Bu kurumdaki öğrenci atamaları sona erer; geçmiş koçluk kayıtları silinmez.`)) return;
    this.changeMembership(this.institutionService.removeTeacher(teacher.userId), 'Öğretmenin kurum ilişkisi sona erdi.', () => {
      this.teachers.update(current => current.filter(item => item.userId !== teacher.userId));
      this.teacherFilterOptions.update(current => current.filter(item => item.userId !== teacher.userId));
      if (this.studentTeacherFilter() === teacher.userId) this.studentTeacherFilter.set('');
      if (this.studentInviteTeacherUserId === teacher.userId) this.studentInviteTeacherUserId = '';
      this.loadTeachers(); this.loadTeacherFilterOptions();
      if (this.students().length) this.loadStudents();
    });
  }
  private changeMembership(operation: Observable<void>, message: string, refresh: () => void): void {
    this.isMembershipSaving.set(true); this.membershipError.set(null); this.membershipSuccess.set(null);
    operation.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isMembershipSaving.set(false))).subscribe({
      next: () => { this.isMembershipSaving.set(false); this.membershipSuccess.set(message); refresh(); this.loadOverview(); },
      error: error => this.membershipError.set(error.status === 403 ? 'Kurum yetkiniz doğrulanamadı. Oturumunuzu yenileyin.' : 'Üye işlemi kaydedilemedi. Kurum ilişkisini ve alanları kontrol edip yeniden deneyin.')
    });
  }
  private prepareMemberNavigation(): boolean {
    if (!this.canLeavePage()) return false;
    this.cancelStudentEdit(); return true;
  }
  hasTeacherOption(id: string): boolean { return this.teacherFilterOptions().some(teacher => teacher.userId === id); }
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
  readonly pendingInvitationsVisible = signal(false);
  readonly invitationRefreshKey = signal(0);
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
    if (!this.canLeavePage()) return;
    this.cancelStudentEdit();
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
    if (!this.canLeavePage()) return;
    this.cancelStudentEdit();
    this.selectedStudent.set(student);
  }

  closeStudentReport(): void {
    if (!this.canLeavePage()) return;
    this.selectedStudent.set(null);
  }

  openTeacherReport(teacher: CoachingInstitutionTeacher): void {
    if (!this.canLeavePage()) return;
    this.cancelStudentEdit();
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
    if (!this.prepareMemberNavigation()) return;
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
    if (!this.prepareMemberNavigation()) return;
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  setTeacherFilter(value: string): void {
    if (!this.prepareMemberNavigation()) return;
    this.studentTeacherFilter.set(value);
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  setGradeFilter(value: string): void {
    if (!this.prepareMemberNavigation()) return;
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
    if (!this.prepareMemberNavigation()) return;
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
    if (!this.prepareMemberNavigation()) return;
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
        this.invitationRefreshKey.update(value => value + 1);
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
        this.invitationRefreshKey.update(value => value + 1);
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

  togglePendingInvitations(): void {
    this.pendingInvitationsVisible.update(visible => !visible);
  }

  trackByUserId(_: number, user: CoachingInstitutionStudent | CoachingInstitutionTeacher): string {
    return user.userId;
  }

  private loadTeacherFilterOptions(append = false): void {
    if (this.isMembershipSaving()) return;
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
        this.teacherFilterOptions.update(current => append
          ? [...new Map([...current, ...page.teachers].map(teacher => [teacher.userId, teacher])).values()] : page.teachers);
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
