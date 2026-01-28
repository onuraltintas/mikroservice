import { Component, inject, signal, effect, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IdentityService, UserDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreateUserModalComponent } from '../../components/create-user-modal/create-user-modal';
import { EditUserModalComponent } from '../../components/edit-user-modal/edit-user-modal';
import { ChangePasswordModalComponent } from '../../components/change-password-modal/change-password-modal';
import { UserDetailsModalComponent } from '../../components/user-details-modal/user-details-modal';
import { RoleManagementModalComponent } from '../../components/role-management-modal/role-management-modal';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, FormsModule, CreateUserModalComponent, EditUserModalComponent, ChangePasswordModalComponent, UserDetailsModalComponent, RoleManagementModalComponent],
  templateUrl: './user-list.html',
  styleUrls: ['./user-list.scss']
})
export class UserListComponent {
  private identityService = inject(IdentityService);
  private toaster = inject(ToasterService);
  private platformId = inject(PLATFORM_ID);

  // State
  users = signal<UserDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);

  // Filters
  currentPage = signal(1);
  pageSize = signal(10);
  searchTerm = signal('');
  selectedRole = signal<string>('');
  selectedStatus = signal<boolean | null>(null);

  showCreateModal = signal(false);
  showEditModal = signal(false);
  showPasswordModal = signal(false);
  showDetailsModal = signal(false);
  showRoleModal = signal(false);
  userToEdit = signal<UserDto | null>(null);
  userForPasswordChange = signal<UserDto | null>(null);
  userForRoleManagement = signal<UserDto | null>(null);
  selectedUserId = signal<string | null>(null);
  openMenuId = signal<string | null>(null);

  constructor() {
    // Only fetch data in browser (SSR has no token)
    if (isPlatformBrowser(this.platformId)) {
      this.loadRoles();
      effect(() => {
        this.loadUsers();
      });
    }
  }



  statuses = [
    { value: null, label: 'Durum: Tümü' },
    { value: true, label: 'Aktif' },
    { value: false, label: 'Pasif' }
  ];

  // Filter Options
  private defaultRoles = [
    { value: 'Student', label: 'Öğrenci' },
    { value: 'Teacher', label: 'Öğretmen' },
    { value: 'InstitutionAdmin', label: 'Kurum Yöneticisi' },
    { value: 'InstitutionOwner', label: 'Kurum Sahibi' },
    { value: 'Parent', label: 'Veli' },
    { value: 'SystemAdmin', label: 'Sistem Yöneticisi' }
  ];

  roleOptions = signal<{ value: string, label: string }[]>([
    { value: '', label: 'Tüm Roller' },
    ...this.defaultRoles
  ]);

  loadRoles() {
    this.identityService.getAllRoles().subscribe({
      next: (roles) => {
        if (roles && roles.length > 0) {
          const dynamicRoles = roles.map(r => ({
            value: r.name,
            label: r.name // r.description yerine r.name kullanılsın
          }));

          const mergedMap = new Map();
          this.defaultRoles.forEach(r => mergedMap.set(r.value, r));
          dynamicRoles.forEach(r => mergedMap.set(r.value, r));

          this.roleOptions.set([{ value: '', label: 'Tüm Roller' }, ...Array.from(mergedMap.values())]);
        } else {
          this.roleOptions.set([{ value: '', label: 'Tüm Roller' }, ...this.defaultRoles]);
        }
      },
      error: (err) => {
        console.error('Failed to load roles', err);
        this.roleOptions.set([{ value: '', label: 'Tüm Roller' }, ...this.defaultRoles]);
      }
    });
  }

  loadUsers() {
    this.loading.set(true);
    this.identityService.getAllUsers(
      this.currentPage(),
      this.pageSize(),
      this.searchTerm(),
      this.selectedRole() || undefined,
      this.selectedStatus() ?? undefined
    ).subscribe({
      next: (res) => {
        this.users.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading users', err);
        this.loading.set(false);
      }
    });
  }

  onSearch(event: any) {
    this.searchTerm.set(event.target.value);
    this.currentPage.set(1); // Reset to first page on search
  }

  onRoleChange(event: any) {
    this.selectedRole.set(event.target.value);
    this.currentPage.set(1);
  }

  onStatusChange(event: any) {
    const val = event.target.value;
    const boolVal = val === 'null' ? null : val === 'true';
    this.selectedStatus.set(boolVal);
    this.currentPage.set(1);
  }

  onPageSizeChange(event: any) {
    this.pageSize.set(Number(event.target.value));
    this.currentPage.set(1);
  }

  openCreateModal() {
    this.showCreateModal.set(true);
  }

  onModalClose(saved: boolean) {
    this.showCreateModal.set(false);
    if (saved) {
      this.loadUsers();
    }
  }

  onEditModalClose(saved: boolean) {
    this.showEditModal.set(false);
    this.userToEdit.set(null);
    if (saved) {
      this.loadUsers();
    }
  }

  onPasswordModalClose(saved: boolean) {
    this.showPasswordModal.set(false);
    this.userForPasswordChange.set(null);
  }

  openPasswordModal(user: UserDto) {
    this.userForPasswordChange.set(user);
    this.showPasswordModal.set(true);
    this.openMenuId.set(null);
  }

  openDetailsModal(userId: string) {
    this.selectedUserId.set(userId);
    this.showDetailsModal.set(true);
    this.openMenuId.set(null);
  }

  onDetailsModalClose() {
    this.showDetailsModal.set(false);
    this.selectedUserId.set(null);
  }

  openRoleModal(user: UserDto) {
    this.userForRoleManagement.set(user);
    this.showRoleModal.set(true);
    this.openMenuId.set(null);
  }

  onRoleModalClose(changed: boolean) {
    this.showRoleModal.set(false);
    this.userForRoleManagement.set(null);
    if (changed) {
      this.loadUsers();
    }
  }

  toggleMenu(userId: string, event: Event) {
    event.stopPropagation();
    if (this.openMenuId() === userId) {
      this.openMenuId.set(null);
    } else {
      this.openMenuId.set(userId);
    }
  }

  @HostListener('document:click')
  closeMenu() {
    this.openMenuId.set(null);
  }

  async deleteUser(userId: string) {
    const confirmed = await this.toaster.confirm(
      'Kullanıcıyı Pasif Yap',
      'Kullanıcıyı pasif duruma getirmek istediğinize emin misiniz?',
      'Evet, pasif yap'
    );

    if (confirmed) {
      this.identityService.deleteUser(userId, false).subscribe({
        next: () => {
          this.users.update(users => users.map(u =>
            u.userId === userId ? { ...u, isActive: false } : u
          ));
          this.openMenuId.set(null);
          this.toaster.success('Kullanıcı pasif duruma getirildi');
        },
        error: (err) => {
          console.error('Delete failed', err);
          this.toaster.error('İşlem başarısız oldu');
        }
      });
    }
  }

  async activateUser(userId: string) {
    const confirmed = await this.toaster.confirm(
      'Kullanıcıyı Aktif Yap',
      'Kullanıcıyı tekrar aktif hale getirmek istediğinize emin misiniz?',
      'Evet, aktif yap'
    );

    if (confirmed) {
      this.identityService.activateUser(userId).subscribe({
        next: () => {
          this.users.update(users => users.map(u =>
            u.userId === userId ? { ...u, isActive: true } : u
          ));
          this.openMenuId.set(null);
          this.toaster.success('Kullanıcı aktif hale getirildi');
        },
        error: (err) => {
          console.error('Activate failed', err);
          this.toaster.error('İşlem başarısız oldu');
        }
      });
    }
  }

  async confirmUserEmail(userId: string) {
    const confirmed = await this.toaster.confirm(
      'E-Postayı Doğrula',
      'Kullanıcının e-postasını manuel olarak doğrulamak istediğinize emin misiniz?',
      'Evet, doğrula'
    );

    if (confirmed) {
      this.identityService.confirmEmail(userId).subscribe({
        next: () => {
          this.users.update(users => users.map(u =>
            u.userId === userId ? { ...u, emailConfirmed: true } : u
          ));
          this.openMenuId.set(null);
          this.toaster.success('E-Posta başarıyla doğrulandı');
        },
        error: (err) => {
          console.error('Email confirmation failed', err);
          this.toaster.error('İşlem başarısız oldu');
        }
      });
    }
  }

  async permanentDeleteUser(userId: string) {
    const confirmed = await this.toaster.confirm(
      'DİKKAT! Kalıcı Silme',
      'Kullanıcıyı kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz.',
      'Evet, kalıcı olarak sil'
    );

    if (confirmed) {
      this.identityService.deleteUser(userId, true).subscribe({
        next: () => {
          this.loadUsers();
          this.openMenuId.set(null);
          this.toaster.success('Kullanıcı başarıyla silindi');
        },
        error: (err) => {
          console.error('Permanent delete failed', err);
          this.toaster.error('Silme işlemi başarısız oldu');
        }
      });
    }
  }

  editUser(user: UserDto) {
    this.userToEdit.set(user);
    this.showEditModal.set(true);
    this.openMenuId.set(null);
  }

  changePage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage.set(page);
  }

  get totalPages() {
    return Math.ceil(this.totalCount() / this.pageSize());
  }

  // Helper for pagination array
  get pagesArray() {
    const total = this.totalPages;
    const current = this.currentPage();
    const delta = 2; // Show 2 pages around current

    // Simple logic for now, could be smarter
    let pages = [];
    for (let i = 1; i <= total; i++) {
      if (i === 1 || i === total || (i >= current - delta && i <= current + delta)) {
        pages.push(i);
      } else if (pages[pages.length - 1] !== '...') {
        pages.push('...');
      }
    }
    return pages;
  }
}
