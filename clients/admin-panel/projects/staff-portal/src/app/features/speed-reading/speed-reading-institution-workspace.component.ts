import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SpeedReadingInstitutionMember,
  SpeedReadingInstitutionMemberFilters,
  SpeedReadingInstitutionMemberRole,
  SpeedReadingInstitutionService
} from './speed-reading-institution.service';
import { SpeedReadingTeacherClassOverview } from './speed-reading-teacher.service';

type MemberStatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'staff-speed-reading-institution-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './speed-reading-institution-workspace.component.html',
  styleUrl: './speed-reading-institution-workspace.component.scss'
})
export class SpeedReadingInstitutionWorkspaceComponent implements OnInit {
  private readonly service = inject(SpeedReadingInstitutionService);
  private memberRequestVersion = 0;
  private overviewRequestVersion = 0;

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

  searchInput = '';
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
      next: institution => {
        this.institution.set(institution);
        this.loadMembers();
        this.loadOverview();
      },
      error: () => {
        this.institutionErrorMessage.set('Kurum bilgisi alınamadı. Kurum yetkinizi kontrol edin ve tekrar deneyin.');
        this.isInstitutionLoading.set(false);
      },
      complete: () => this.isInstitutionLoading.set(false)
    });
  }

  selectRole(role: SpeedReadingInstitutionMemberRole): void {
    if (this.selectedRole() === role) return;
    this.selectedRole.set(role);
    this.pageNumber.set(1);
    this.loadMembers();
  }

  loadMembers(): void {
    const institution = this.institution();
    if (!institution) return;

    const requestVersion = ++this.memberRequestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.service.getMembers(institution.institutionId, this.selectedRole(), this.pageNumber(), 25, this.filters()).subscribe({
      next: page => {
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
      }
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
      next: overview => {
        if (requestVersion === this.overviewRequestVersion) this.overview.set(overview);
      },
      error: () => {
        if (requestVersion !== this.overviewRequestVersion) return;
        this.overviewErrorMessage.set('Kurum özeti yüklenemedi. Lütfen tekrar deneyin.');
        this.isOverviewLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.overviewRequestVersion) this.isOverviewLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    const filters: SpeedReadingInstitutionMemberFilters = {};
    const searchTerm = this.searchInput.trim();
    const gradeLevel = Number(this.selectedGradeLevel);
    if (searchTerm) filters.searchTerm = searchTerm;
    if (this.selectedRole() === 'Student' && Number.isInteger(gradeLevel) && gradeLevel >= 1 && gradeLevel <= 12) {
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

  previousPage(): void {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.loadMembers();
  }

  nextPage(): void {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.loadMembers();
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 }).format(value);
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' }).format(date);
  }

  trackById(_: number, member: SpeedReadingInstitutionMember): string {
    return member.userId;
  }
}
