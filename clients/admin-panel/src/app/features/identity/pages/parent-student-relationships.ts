import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize, forkJoin } from 'rxjs';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { AuthService, hasRequiredAccess } from '../../../core/auth/auth.service';
import { getAdminErrorMessage } from '../../../core/auth/admin-error-message';
import {
  IdentityService,
  ParentRelationship,
  ParentStudentRelationshipDto,
  ParentStudentRelationshipStatus,
  UserDto
} from '../../../core/services/identity.service';

@Component({
  selector: 'app-parent-student-relationships',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './parent-student-relationships.html'
})
export class ParentStudentRelationshipsComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  private readonly auth = inject(AuthService);

  readonly canEdit = computed(() => hasRequiredAccess(
    this.auth.userProfile(), ADMIN_PERMISSIONS.usersEdit, 'SystemAdmin'));
  readonly items = signal<ParentStudentRelationshipDto[]>([]);
  readonly parents = signal<UserDto[]>([]);
  readonly students = signal<UserDto[]>([]);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly totalCount = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize = 25;

  search = '';
  status: ParentStudentRelationshipStatus | '' = '';
  parentUserId = '';
  studentUserId = '';
  relationship: ParentRelationship = 'Mother';
  revokeTarget: ParentStudentRelationshipDto | null = null;
  revocationReason = '';

  ngOnInit(): void {
    this.load();
    if (this.canEdit()) this.loadDirectories();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.identity.getParentStudentRelationships(
      this.currentPage(), this.pageSize, this.search, this.status || undefined)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: page => {
          this.items.set(page.items);
          this.totalCount.set(page.totalCount);
        },
        error: error => this.error.set(getAdminErrorMessage(
          error, 'Veli–öğrenci ilişkileri yüklenemedi.'))
      });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1 || (page - 1) * this.pageSize >= this.totalCount()) return;
    this.currentPage.set(page);
    this.load();
  }

  create(): void {
    if (!this.parentUserId || !this.studentUserId || this.submitting()) return;
    this.runMutation(
      this.identity.requestParentStudentRelationship(
        this.parentUserId, this.studentUserId, this.relationship),
      'İlişki talebi oluşturuldu.',
      () => {
        this.parentUserId = '';
        this.studentUserId = '';
      });
  }

  verify(item: ParentStudentRelationshipDto): void {
    this.runMutation(
      this.identity.verifyParentStudentRelationship(item.id),
      'İlişki doğrulandı.');
  }

  openRevoke(item: ParentStudentRelationshipDto): void {
    this.revokeTarget = item;
    this.revocationReason = '';
  }

  cancelRevoke(): void {
    this.revokeTarget = null;
    this.revocationReason = '';
  }

  revoke(): void {
    const reason = this.revocationReason.trim();
    if (!this.revokeTarget || !reason || reason.length > 500 || this.submitting()) return;
    this.runMutation(
      this.identity.revokeParentStudentRelationship(this.revokeTarget.id, reason),
      'İlişki iptal edildi.',
      () => this.cancelRevoke());
  }

  relationshipLabel(value: ParentRelationship): string {
    return ({ Mother: 'Anne', Father: 'Baba', Guardian: 'Vasi', Other: 'Diğer' })[value];
  }

  statusLabel(value: ParentStudentRelationshipStatus): string {
    return ({ Pending: 'Bekliyor', Verified: 'Doğrulandı', Revoked: 'İptal edildi' })[value];
  }

  private loadDirectories(): void {
    forkJoin({
      parents: this.identity.getAllUsers(1, 100, '', 'Parent', true),
      students: this.identity.getAllUsers(1, 100, '', 'Student', true)
    }).subscribe({
      next: result => {
        this.parents.set(result.parents.items);
        this.students.set(result.students.items);
      },
      error: error => this.error.set(getAdminErrorMessage(
        error, 'Veli ve öğrenci seçenekleri yüklenemedi.'))
    });
  }

  private runMutation(request: Observable<unknown>, message: string, after?: () => void): void {
    this.submitting.set(true);
    this.error.set('');
    this.success.set('');
    request.pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => {
        this.success.set(message);
        after?.();
        this.load();
      },
      error: error => this.error.set(getAdminErrorMessage(
        error, 'İşlem tamamlanamadı. MFA oturumunuzu ve yetkinizi kontrol edin.', true))
    });
  }
}
