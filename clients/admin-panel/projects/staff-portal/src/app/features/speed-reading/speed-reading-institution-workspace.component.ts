import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import {
  SpeedReadingInstitutionAssignmentReport,
  SpeedReadingInstitutionContentReport,
  SpeedReadingInstitutionMember,
  SpeedReadingInstitutionMemberFilters,
  SpeedReadingInstitutionMemberRole,
  SpeedReadingInstitutionProgressReport,
  SpeedReadingInstitutionService,
} from './speed-reading-institution.service';
import { SpeedReadingTeacherClassOverview } from './speed-reading-teacher.service';
import { SpeedReadingStudentReportComponent } from './speed-reading-institution-student-report.component';

type MemberStatusFilter = 'all' | 'active' | 'inactive';
type TeacherOption = { userId: string; displayName: string };
type MemberStatusChange = {
  userId: string;
  role: SpeedReadingInstitutionMemberRole;
  displayName: string;
  isActive: boolean;
};
type InstitutionReportTab = 'assignments' | 'content' | 'progress';

function utcDateInput(daysAgo: number): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() - daysAgo);
  return date.toISOString().slice(0, 10);
}

@Component({
  selector: 'staff-speed-reading-institution-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, SpeedReadingStudentReportComponent],
  templateUrl: './speed-reading-institution-workspace.component.html',
  styleUrl: './speed-reading-institution-workspace.component.scss',
})
export class SpeedReadingInstitutionWorkspaceComponent implements OnInit {
  private readonly service = inject(SpeedReadingInstitutionService);
  private memberRequestVersion = 0;
  private overviewRequestVersion = 0;
  private teacherSearchVersion = 0;
  private institutionReportRequestVersion = 0;

  readonly institution = signal<{ institutionId: string; institutionName: string } | null>(null);
  readonly members = signal<SpeedReadingInstitutionMember[]>([]);
  readonly overview = signal<SpeedReadingTeacherClassOverview | null>(null);
  readonly selectedRole = signal<SpeedReadingInstitutionMemberRole>('Student');
  readonly isInstitutionLoading = signal(true);
  readonly isLoading = signal(false);
  readonly isOverviewLoading = signal(false);
  readonly institutionErrorMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly overviewErrorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly filters = signal<SpeedReadingInstitutionMemberFilters>({});
  readonly editingStudent = signal<SpeedReadingInstitutionMember | null>(null);
  readonly teacherOptions = signal<TeacherOption[]>([]);
  readonly pendingStatusChange = signal<MemberStatusChange | null>(null);
  readonly isTeacherSearchLoading = signal(false);
  readonly isStudentProfileSaving = signal(false);
  readonly isMemberStatusSaving = signal(false);
  readonly isInvitationSending = signal(false);
  readonly teacherSearchErrorMessage = signal<string | null>(null);
  readonly studentProfileErrorMessage = signal<string | null>(null);
  readonly memberStatusErrorMessage = signal<string | null>(null);
  readonly selectedStudentReport = signal<SpeedReadingInstitutionMember | null>(null);
  readonly selectedInstitutionReport = signal<InstitutionReportTab | null>(null);
  readonly assignmentReport = signal<SpeedReadingInstitutionAssignmentReport | null>(null);
  readonly contentReport = signal<SpeedReadingInstitutionContentReport | null>(null);
  readonly progressReport = signal<SpeedReadingInstitutionProgressReport | null>(null);
  readonly isInstitutionReportLoading = signal(false);
  readonly institutionReportErrorMessage = signal<string | null>(null);
  readonly invitationErrorMessage = signal<string | null>(null);
  readonly invitationSuccessMessage = signal<string | null>(null);

  searchInput = '';
  invitationEmail = '';
  teacherSearchInput = '';
  reportDateFrom = utcDateInput(29);
  reportDateTo = utcDateInput(0);
  studentGradeLevel = '';
  studentTeacherUserId = '';
  selectedGradeLevel = '';
  selectedStatus: MemberStatusFilter = 'all';
  readonly gradeLevels = Array.from({ length: 12 }, (_, index) => index + 1);

  ngOnInit(): void {
    this.loadInstitution();
  }

  loadInstitution(): void {
    this.isInstitutionLoading.set(true);
    this.institutionErrorMessage.set(null);
    this.service.getMyInstitution().subscribe({
      next: (institution) => {
        this.institution.set(institution);
        this.loadMembers();
        this.loadOverview();
      },
      error: () => {
        this.institutionErrorMessage.set(
          'Kurum bilgisi alınamadı. Kurum yetkinizi kontrol edin ve tekrar deneyin.',
        );
        this.isInstitutionLoading.set(false);
      },
      complete: () => this.isInstitutionLoading.set(false),
    });
  }

  selectRole(role: SpeedReadingInstitutionMemberRole): void {
    if (this.selectedRole() === role) return;
    this.selectedRole.set(role);
    this.invitationErrorMessage.set(null);
    this.invitationSuccessMessage.set(null);
    this.pageNumber.set(1);
    this.editingStudent.set(null);
    this.teacherOptions.set([]);
    this.pendingStatusChange.set(null);
    this.selectedStudentReport.set(null);
    this.studentProfileErrorMessage.set(null);
    this.memberStatusErrorMessage.set(null);
    this.loadMembers();
  }

  loadMembers(): void {
    const institution = this.institution();
    if (!institution) return;

    const requestVersion = ++this.memberRequestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.service
      .getMembers(
        institution.institutionId,
        this.selectedRole(),
        this.pageNumber(),
        25,
        this.filters(),
      )
      .subscribe({
        next: (page) => {
          if (requestVersion !== this.memberRequestVersion) return;
          this.members.set(page.items);
          this.totalCount.set(page.totalCount);
          this.totalPages.set(Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
        },
        error: () => {
          if (requestVersion !== this.memberRequestVersion) return;
          this.errorMessage.set('Kurum üye listesi yüklenemedi. Lütfen tekrar deneyin.');
          this.isLoading.set(false);
        },
        complete: () => {
          if (requestVersion === this.memberRequestVersion) this.isLoading.set(false);
        },
      });
  }

  loadOverview(): void {
    const institution = this.institution();
    if (!institution) return;

    const requestVersion = ++this.overviewRequestVersion;
    const dateTo = new Date();
    const dateFrom = new Date(dateTo);
    dateFrom.setUTCDate(dateFrom.getUTCDate() - 29);
    dateFrom.setUTCHours(0, 0, 0, 0);
    this.isOverviewLoading.set(true);
    this.overviewErrorMessage.set(null);
    this.service.getClassOverview(institution.institutionId, dateFrom, dateTo).subscribe({
      next: (overview) => {
        if (requestVersion === this.overviewRequestVersion) this.overview.set(overview);
      },
      error: () => {
        if (requestVersion !== this.overviewRequestVersion) return;
        this.overviewErrorMessage.set('Kurum özeti yüklenemedi. Lütfen tekrar deneyin.');
        this.isOverviewLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.overviewRequestVersion) this.isOverviewLoading.set(false);
      },
    });
  }

  sendInvitation(): void {
    const institution = this.institution();
    const email = this.invitationEmail.trim();
    const role = this.selectedRole();
    if (!institution || !email || this.isInvitationSending()) return;

    this.isInvitationSending.set(true);
    this.invitationErrorMessage.set(null);
    this.invitationSuccessMessage.set(null);
    this.service.inviteMember(institution.institutionId, email, role).subscribe({
      next: () => {
        if (this.invitationEmail.trim() === email) this.invitationEmail = '';
        this.invitationSuccessMessage.set(
          `${role === 'Student' ? 'Öğrenci' : 'Öğretmen'} daveti gönderildi. Davet edilen kişi aynı e-posta adresiyle Hızlı Okuma hesabı oluşturup daveti kabul edebilir.`,
        );
      },
      error: (error) => {
        this.invitationErrorMessage.set(
          this.getApiMessage(
            error,
            `${role === 'Student' ? 'Öğrenci' : 'Öğretmen'} daveti gönderilemedi. E-posta adresini ve Hızlı Okuma kurum yetkinizi kontrol edip yeniden deneyin.`,
          ),
        );
        this.isInvitationSending.set(false);
      },
      complete: () => this.isInvitationSending.set(false),
    });
  }

  applyFilters(): void {
    const filters: SpeedReadingInstitutionMemberFilters = {};
    const searchTerm = this.searchInput.trim();
    const gradeLevel = Number(this.selectedGradeLevel);
    if (searchTerm) filters.searchTerm = searchTerm;
    if (
      this.selectedRole() === 'Student' &&
      Number.isInteger(gradeLevel) &&
      gradeLevel >= 1 &&
      gradeLevel <= 12
    ) {
      filters.gradeLevel = gradeLevel;
    }
    if (this.selectedStatus !== 'all') filters.isActive = this.selectedStatus === 'active';
    this.filters.set(filters);
    this.pageNumber.set(1);
    this.loadMembers();
  }

  clearFilters(): void {
    this.searchInput = '';
    this.selectedGradeLevel = '';
    this.selectedStatus = 'all';
    this.filters.set({});
    this.pageNumber.set(1);
    this.loadMembers();
  }

  startStudentEdit(member: SpeedReadingInstitutionMember): void {
    if (member.role !== 'Student' || this.isStudentProfileSaving() || this.isMemberStatusSaving())
      return;
    this.pendingStatusChange.set(null);
    this.selectedStudentReport.set(null);
    this.memberStatusErrorMessage.set(null);
    this.editingStudent.set(member);
    this.studentGradeLevel = member.gradeLevel?.toString() ?? '';
    this.studentTeacherUserId = member.teacherUserId ?? '';
    this.teacherSearchInput = '';
    this.teacherSearchErrorMessage.set(null);
    this.studentProfileErrorMessage.set(null);
    this.teacherOptions.set(
      member.teacherUserId
        ? [
            {
              userId: member.teacherUserId,
              displayName: member.teacherName || 'Mevcut atanmış öğretmen',
            },
          ]
        : [],
    );
  }

  cancelStudentEdit(): void {
    if (this.isStudentProfileSaving()) return;
    this.editingStudent.set(null);
    this.teacherOptions.set([]);
    this.studentProfileErrorMessage.set(null);
  }

  openStudentReport(member: SpeedReadingInstitutionMember): void {
    if (member.role !== 'Student' || this.isStudentProfileSaving() || this.isMemberStatusSaving())
      return;
    this.editingStudent.set(null);
    this.pendingStatusChange.set(null);
    this.teacherOptions.set([]);
    this.selectedStudentReport.set(member);
  }

  closeStudentReport(): void {
    this.selectedStudentReport.set(null);
  }

  selectInstitutionReport(tab: InstitutionReportTab): void {
    this.selectedInstitutionReport.set(tab);
    this.loadSelectedInstitutionReport();
  }

  applyInstitutionReportRange(): void {
    if (this.selectedInstitutionReport()) this.loadSelectedInstitutionReport();
  }

  retryInstitutionReport(): void {
    if (this.selectedInstitutionReport()) this.loadSelectedInstitutionReport();
  }

  private loadSelectedInstitutionReport(): void {
    const institution = this.institution();
    const tab = this.selectedInstitutionReport();
    if (!institution || !tab) return;

    const dateFrom = this.parseUtcDate(this.reportDateFrom, false);
    const dateTo = this.parseUtcDate(this.reportDateTo, true);
    const dateRangeTooLarge =
      dateFrom !== null &&
      dateTo !== null &&
      dateTo.getTime() - dateFrom.getTime() > 366 * 24 * 60 * 60 * 1000;
    if (!dateFrom || !dateTo || dateFrom > dateTo || dateRangeTooLarge) {
      this.institutionReportRequestVersion++;
      if (tab === 'assignments') this.assignmentReport.set(null);
      if (tab === 'content') this.contentReport.set(null);
      if (tab === 'progress') this.progressReport.set(null);
      this.institutionReportErrorMessage.set(
        dateRangeTooLarge
          ? 'Rapor tarih aralığı en fazla 366 gün olabilir.'
          : 'Rapor tarih aralığını kontrol edin; başlangıç tarihi bitiş tarihinden sonra olamaz.',
      );
      this.isInstitutionReportLoading.set(false);
      return;
    }

    this.institutionReportErrorMessage.set(null);
    if (tab === 'assignments') {
      this.assignmentReport.set(null);
      this.loadReport(
        this.service.getInstitutionAssignments(institution.institutionId, dateFrom, dateTo),
        (report) => this.assignmentReport.set(report),
      );
    } else if (tab === 'content') {
      this.contentReport.set(null);
      this.loadReport(
        this.service.getInstitutionContentAnalysis(institution.institutionId, dateFrom, dateTo),
        (report) => this.contentReport.set(report),
      );
    } else {
      this.progressReport.set(null);
      this.loadReport(
        this.service.getInstitutionTimeProgress(institution.institutionId, dateFrom, dateTo),
        (report) => this.progressReport.set(report),
      );
    }
  }

  private loadReport<T>(request: Observable<T>, save: (report: T) => void): void {
    const requestVersion = ++this.institutionReportRequestVersion;
    this.isInstitutionReportLoading.set(true);
    request.subscribe({
      next: (report) => {
        if (requestVersion === this.institutionReportRequestVersion) save(report);
      },
      error: () => {
        if (requestVersion !== this.institutionReportRequestVersion) return;
        this.institutionReportErrorMessage.set('Kurum raporu yüklenemedi. Lütfen tekrar deneyin.');
        this.isInstitutionReportLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.institutionReportRequestVersion)
          this.isInstitutionReportLoading.set(false);
      },
    });
  }

  private parseUtcDate(value: string, endOfDay: boolean): Date | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}Z`);
    return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value ? date : null;
  }

  searchInstitutionTeachers(): void {
    const institution = this.institution();
    if (!institution) return;
    const searchTerm = this.teacherSearchInput.trim();
    if (searchTerm.length < 2) {
      this.teacherSearchErrorMessage.set('Öğretmen aramak için en az iki karakter yazın.');
      return;
    }

    const requestVersion = ++this.teacherSearchVersion;
    this.isTeacherSearchLoading.set(true);
    this.teacherSearchErrorMessage.set(null);
    this.service
      .getMembers(institution.institutionId, 'Teacher', 1, 25, { searchTerm, isActive: true })
      .subscribe({
        next: (page) => {
          if (requestVersion !== this.teacherSearchVersion) return;
          const options = page.items.map((teacher) => ({
            userId: teacher.userId,
            displayName: `${teacher.firstName} ${teacher.lastName}`.trim(),
          }));
          const selectedTeacher = this.teacherOptions().find(
            (option) => option.userId === this.studentTeacherUserId,
          );
          if (
            selectedTeacher &&
            !options.some((option) => option.userId === selectedTeacher.userId)
          ) {
            options.unshift(selectedTeacher);
          }
          this.teacherOptions.set(options);
          if (options.length === 0)
            this.teacherSearchErrorMessage.set('Eşleşen etkin Hızlı Okuma öğretmeni bulunamadı.');
        },
        error: () => {
          if (requestVersion !== this.teacherSearchVersion) return;
          this.teacherSearchErrorMessage.set('Öğretmen araması yapılamadı. Lütfen tekrar deneyin.');
          this.isTeacherSearchLoading.set(false);
        },
        complete: () => {
          if (requestVersion === this.teacherSearchVersion) this.isTeacherSearchLoading.set(false);
        },
      });
  }

  saveStudentProfile(): void {
    const institution = this.institution();
    const student = this.editingStudent();
    if (!institution || !student || this.isStudentProfileSaving()) return;
    const gradeLevel = this.studentGradeLevel ? Number(this.studentGradeLevel) : null;
    if (
      gradeLevel !== null &&
      (!Number.isInteger(gradeLevel) || gradeLevel < 1 || gradeLevel > 12)
    ) {
      this.studentProfileErrorMessage.set('Sınıf seviyesi 1 ile 12 arasında olmalıdır.');
      return;
    }

    this.isStudentProfileSaving.set(true);
    this.studentProfileErrorMessage.set(null);
    this.service
      .updateStudentProfile(
        institution.institutionId,
        student.userId,
        gradeLevel,
        this.studentTeacherUserId || null,
      )
      .subscribe({
        next: () => {
          this.editingStudent.set(null);
          this.teacherOptions.set([]);
          this.studentProfileErrorMessage.set(null);
          this.loadMembers();
        },
        error: (error) => {
          this.studentProfileErrorMessage.set(
            this.getApiMessage(
              error,
              'Öğrenci bilgileri kaydedilemedi. Kurum üyeliğini ve öğretmen atamasını kontrol edin.',
            ),
          );
          this.isStudentProfileSaving.set(false);
        },
        complete: () => this.isStudentProfileSaving.set(false),
      });
  }

  requestMemberStatusChange(member: SpeedReadingInstitutionMember): void {
    if (this.isStudentProfileSaving() || this.isMemberStatusSaving()) return;
    this.editingStudent.set(null);
    this.selectedStudentReport.set(null);
    this.teacherOptions.set([]);
    this.studentProfileErrorMessage.set(null);
    this.pendingStatusChange.set({
      userId: member.userId,
      role: member.role,
      displayName: `${member.firstName} ${member.lastName}`.trim(),
      isActive: !member.isMembershipActive,
    });
    this.memberStatusErrorMessage.set(null);
  }

  cancelMemberStatusChange(): void {
    if (this.isMemberStatusSaving()) return;
    this.pendingStatusChange.set(null);
    this.memberStatusErrorMessage.set(null);
  }

  confirmMemberStatusChange(): void {
    const institution = this.institution();
    const change = this.pendingStatusChange();
    if (!institution || !change || this.isMemberStatusSaving()) return;

    this.isMemberStatusSaving.set(true);
    this.memberStatusErrorMessage.set(null);
    this.service
      .setMemberStatus(institution.institutionId, change.userId, change.role, change.isActive)
      .subscribe({
        next: () => {
          this.pendingStatusChange.set(null);
          this.loadMembers();
          this.loadOverview();
        },
        error: (error) => {
          this.memberStatusErrorMessage.set(
            this.getApiMessage(
              error,
              'Üyelik durumu değiştirilemedi. Hızlı Okuma ürün rolünü kontrol edin.',
            ),
          );
          this.isMemberStatusSaving.set(false);
        },
        complete: () => this.isMemberStatusSaving.set(false),
      });
  }

  previousPage(): void {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update((page) => page - 1);
    this.loadMembers();
  }

  nextPage(): void {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update((page) => page + 1);
    this.loadMembers();
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 }).format(value);
  }

  formatReportNumber(value: number): string {
    return new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 }).format(value);
  }

  reportSeriesLabel(series: { name: string; value: number }[]): string {
    return (
      series.map((item) => `${item.name}: ${this.formatReportNumber(item.value)}`).join(' · ') ||
      '—'
    );
  }

  assignmentStatusLabel(status: string): string {
    switch (status) {
      case 'completed':
        return 'Tamamlandı';
      case 'in-progress':
        return 'Devam ediyor';
      case 'not-started':
        return 'Başlamadı';
      default:
        return status || 'Bilinmiyor';
    }
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    }).format(date);
  }

  trackById(_: number, member: SpeedReadingInstitutionMember): string {
    return member.userId;
  }

  membershipStatusLabel(member: SpeedReadingInstitutionMember): string {
    if (!member.isMembershipActive) return 'Üyelik kapalı';
    return member.isActive ? 'Aktif' : 'Hesap/profil pasif';
  }

  private getApiMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
      const body = error.error as { message?: unknown; description?: unknown };
      if (typeof body.message === 'string') return body.message;
      if (typeof body.description === 'string') return body.description;
    }
    return fallback;
  }
}
