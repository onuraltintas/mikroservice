import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import {
  SpeedReadingInstitutionMember,
  SpeedReadingInstitutionMemberFilters,
  SpeedReadingInstitutionMemberRole,
  SpeedReadingInstitutionService,
} from './speed-reading-institution.service';
import { SpeedReadingTeacherClassOverview } from './speed-reading-teacher.service';

type MemberStatusFilter = 'all' | 'active' | 'inactive';
type TeacherOption = { userId: string; displayName: string };
type MemberStatusChange = {
  userId: string;
  role: SpeedReadingInstitutionMemberRole;
  displayName: string;
  isActive: boolean;
};

@Component({
  selector: 'staff-speed-reading-institution-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './speed-reading-institution-workspace.component.html',
  styleUrl: './speed-reading-institution-workspace.component.scss',
})
export class SpeedReadingInstitutionWorkspaceComponent implements OnInit {
  private readonly service = inject(SpeedReadingInstitutionService);
  private memberRequestVersion = 0;
  private overviewRequestVersion = 0;
  private teacherSearchVersion = 0;

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
  readonly teacherSearchErrorMessage = signal<string | null>(null);
  readonly studentProfileErrorMessage = signal<string | null>(null);
  readonly memberStatusErrorMessage = signal<string | null>(null);

  searchInput = '';
  teacherSearchInput = '';
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
    this.pageNumber.set(1);
    this.editingStudent.set(null);
    this.teacherOptions.set([]);
    this.pendingStatusChange.set(null);
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
