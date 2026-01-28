import { Component, inject, signal, effect, computed, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { IdentityService, RoleDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreateRoleModalComponent } from '../../components/create-role-modal/create-role-modal';
import { EditRoleModalComponent } from '../../components/edit-role-modal/edit-role-modal';
import { RolePermissionsModalComponent } from '../../components/role-permissions-modal/role-permissions-modal';
import { FormsModule } from '@angular/forms';

@Component({
    selector: 'app-role-list',
    standalone: true,
    imports: [CommonModule, FormsModule, CreateRoleModalComponent, EditRoleModalComponent, RolePermissionsModalComponent],
    templateUrl: './role-list.html',
    styleUrls: ['./role-list.scss']
})
export class RoleListComponent {
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);
    private platformId = inject(PLATFORM_ID);

    // State
    roles = signal<RoleDto[]>([]);
    loading = signal(false);

    // Filters & Pagination
    searchTerm = signal('');
    statusFilter = signal<'all' | 'active' | 'passive'>('all');
    currentPage = signal(1);
    pageSize = signal(10);

    filteredRoles = computed(() => {
        let result = this.roles();
        const term = this.searchTerm().toLowerCase();
        const status = this.statusFilter();

        if (term) {
            result = result.filter(r =>
                r.name.toLowerCase().includes(term) ||
                r.description?.toLowerCase().includes(term)
            );
        }

        if (status === 'active') {
            result = result.filter(r => !r.isDeleted);
        } else if (status === 'passive') {
            result = result.filter(r => r.isDeleted);
        }

        return result;
    });

    paginatedRoles = computed(() => {
        const roles = this.filteredRoles();
        const start = (this.currentPage() - 1) * this.pageSize();
        const end = start + this.pageSize();
        return roles.slice(start, end);
    });

    totalCount = computed(() => this.filteredRoles().length);

    get totalPages() {
        return Math.ceil(this.totalCount() / this.pageSize());
    }

    get pagesArray() {
        const total = this.totalPages;
        const current = this.currentPage();
        const delta = 2;

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

    showCreateModal = signal(false);
    showEditModal = signal(false);
    showPermissionsModal = signal(false);
    roleToEdit = signal<RoleDto | null>(null);
    roleForPermissions = signal<RoleDto | null>(null);

    openMenuId = signal<string | null>(null);

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            this.loadRoles();
        }
    }

    onSearch(event: Event) {
        const target = event.target as HTMLInputElement;
        this.searchTerm.set(target.value);
        this.currentPage.set(1);
    }

    onStatusChange(event: Event) {
        const target = event.target as HTMLSelectElement;
        this.statusFilter.set(target.value as 'all' | 'active' | 'passive');
        this.currentPage.set(1);
    }

    onPageSizeChange(event: Event) {
        const target = event.target as HTMLSelectElement;
        this.pageSize.set(Number(target.value));
        this.currentPage.set(1);
    }

    changePage(page: number) {
        if (page < 1 || page > this.totalPages) return;
        this.currentPage.set(page);
    }

    loadRoles() {
        this.loading.set(true);
        this.identityService.getAllRoles().subscribe({
            next: (data) => {
                this.roles.set(data);
                this.loading.set(false);
            },
            error: (err) => {
                console.error('Error loading roles', err);
                this.loading.set(false);
            }
        });
    }

    openCreateModal() {
        this.showCreateModal.set(true);
    }

    onCreateModalClose(saved: boolean) {
        this.showCreateModal.set(false);
        if (saved) {
            this.loadRoles();
        }
    }

    editRole(role: RoleDto) {
        this.roleToEdit.set(role);
        this.showEditModal.set(true);
        this.openMenuId.set(null);
    }

    onEditModalClose(saved: boolean) {
        this.showEditModal.set(false);
        this.roleToEdit.set(null);
        if (saved) {
            this.loadRoles();
        }
    }

    openPermissionsModal(role: RoleDto) {
        this.roleForPermissions.set(role);
        this.showPermissionsModal.set(true);
        this.openMenuId.set(null);
    }

    onPermissionsModalClose(saved: boolean) {
        this.showPermissionsModal.set(false);
        this.roleForPermissions.set(null);
    }

    toggleMenu(roleId: string, event: Event) {
        event.stopPropagation();
        if (this.openMenuId() === roleId) {
            this.openMenuId.set(null);
        } else {
            this.openMenuId.set(roleId);
        }
    }

    @HostListener('document:click')
    closeMenu() {
        this.openMenuId.set(null);
    }

    async deleteRole(role: RoleDto) {
        if (role.isSystemRole && !role.isDeleted) {
            this.toaster.warning('Sistem rolleri silinemez.');
            return;
        }

        const confirmed = await this.toaster.confirm(
            'Rolü Pasife Al',
            'Bu rolü pasife almak istediğinize emin misiniz? (Daha sonra kalıcı olarak silebilirsiniz)',
            'Evet, Pasife Al'
        );

        if (confirmed) {
            this.identityService.deleteRoleById(role.id, false).subscribe({
                next: () => {
                    this.loadRoles();
                    this.openMenuId.set(null);
                    this.toaster.success('Rol pasife alındı.');
                },
                error: (err) => {
                    console.error('Delete role failed', err);
                    this.toaster.error('Rol pasife alınamadı');
                }
            });
        }
    }

    async restoreRole(role: RoleDto) {
        const confirmed = await this.toaster.confirm(
            'Rolü Aktif Yap',
            'Bu rolü tekrar aktif hale getirmek istediğinize emin misiniz?',
            'Evet, Aktif Yap'
        );

        if (confirmed) {
            this.identityService.restoreRole(role.id).subscribe({
                next: () => {
                    this.loadRoles();
                    this.openMenuId.set(null);
                    this.toaster.success('Rol tekrar aktif edildi.');
                },
                error: (err) => {
                    console.error('Restore role failed', err);
                    this.toaster.error('Rol aktif edilemedi');
                }
            });
        }
    }

    async permanentDeleteRole(role: RoleDto) {
        const confirmed = await this.toaster.confirm(
            'DİKKAT! Kalıcı Silme',
            'Bu rolü kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz.',
            'Evet, Kalıcı Sil'
        );

        if (confirmed) {
            this.identityService.deleteRoleById(role.id, true).subscribe({
                next: () => {
                    this.loadRoles();
                    this.openMenuId.set(null);
                    this.toaster.success('Rol kalıcı olarak silindi.');
                },
                error: (err) => {
                    console.error('Permanent delete role failed', err);
                    this.toaster.error('Rol silinemedi');
                }
            });
        }
    }
}
