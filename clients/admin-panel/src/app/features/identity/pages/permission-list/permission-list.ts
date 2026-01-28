import { Component, inject, signal, computed, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { IdentityService, PermissionDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreatePermissionModalComponent } from '../../components/create-permission-modal/create-permission-modal';
import { EditPermissionModalComponent } from '../../components/edit-permission-modal/edit-permission-modal';
import { FormsModule } from '@angular/forms';

@Component({
    selector: 'app-permission-list',
    standalone: true,
    imports: [CommonModule, FormsModule, CreatePermissionModalComponent, EditPermissionModalComponent],
    templateUrl: './permission-list.html',
    styleUrls: ['./permission-list.scss']
})
export class PermissionListComponent {
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);
    private platformId = inject(PLATFORM_ID);

    permissions = signal<PermissionDto[]>([]);
    loading = signal(false);

    // Filters
    searchTerm = signal('');
    statusFilter = signal<'all' | 'active' | 'passive'>('all');
    groupFilter = signal<string>('all');
    currentPage = signal(1);
    pageSize = signal(10);

    groups = computed(() => {
        const perms = this.permissions();
        const uniqueGroups = new Set(perms.map(p => p.group).filter(g => !!g));
        return Array.from(uniqueGroups).sort();
    });

    filteredPermissions = computed(() => {
        let result = this.permissions();
        const term = this.searchTerm().toLowerCase();
        const status = this.statusFilter();
        const group = this.groupFilter();

        if (term) {
            result = result.filter(r =>
                r.key.toLowerCase().includes(term) ||
                r.description?.toLowerCase().includes(term) ||
                r.group?.toLowerCase().includes(term)
            );
        }

        if (group && group !== 'all') {
            result = result.filter(r => r.group === group);
        }

        if (status === 'active') {
            result = result.filter(r => !r.isDeleted);
        } else if (status === 'passive') {
            result = result.filter(r => r.isDeleted);
        }

        return result;
    });

    paginatedPermissions = computed(() => {
        const perms = this.filteredPermissions();
        const start = (this.currentPage() - 1) * this.pageSize();
        const end = start + this.pageSize();
        return perms.slice(start, end);
    });

    totalCount = computed(() => this.filteredPermissions().length);

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
    permissionToEdit = signal<PermissionDto | null>(null);

    openMenuId = signal<string | null>(null);

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            this.loadPermissions();
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

    onGroupChange(event: Event) {
        const target = event.target as HTMLSelectElement;
        this.groupFilter.set(target.value);
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

    loadPermissions() {
        this.loading.set(true);
        this.identityService.getPermissions().subscribe({
            next: (data) => {
                this.permissions.set(data);
                this.loading.set(false);
            },
            error: (err) => {
                console.error('Error loading permissions', err);
                this.toaster.error('İzinler yüklenemedi');
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
            this.loadPermissions();
        }
    }

    editPermission(permission: PermissionDto) {
        this.permissionToEdit.set(permission);
        this.showEditModal.set(true);
        this.openMenuId.set(null);
    }

    onEditModalClose(saved: boolean) {
        this.showEditModal.set(false);
        this.permissionToEdit.set(null);
        if (saved) {
            this.loadPermissions();
        }
    }

    toggleMenu(id: string, event: Event) {
        event.stopPropagation();
        if (this.openMenuId() === id) {
            this.openMenuId.set(null);
        } else {
            this.openMenuId.set(id);
        }
    }

    @HostListener('document:click')
    closeMenu() {
        this.openMenuId.set(null);
    }

    async deletePermission(permission: PermissionDto) {
        if (permission.isSystem && !permission.isDeleted) {
            this.toaster.warning('Sistem izinleri silinemez.');
            return;
        }

        const confirmed = await this.toaster.confirm(
            'İzni Pasife Al',
            'Bu izni pasife almak istediğinize emin misiniz? (Daha sonra kalıcı olarak silebilirsiniz)',
            'Evet, Pasife Al'
        );

        if (confirmed) {
            this.identityService.deletePermission(permission.id, false).subscribe({
                next: () => {
                    this.loadPermissions();
                    this.openMenuId.set(null);
                    this.toaster.success('İzin pasife alındı.');
                },
                error: (err) => {
                    console.error('Delete permission failed', err);
                    this.toaster.error('İzin pasife alınamadı');
                }
            });
        }
    }

    async restorePermission(permission: PermissionDto) {
        const confirmed = await this.toaster.confirm(
            'İzni Aktif Yap',
            'Bu izni tekrar aktif hale getirmek istediğinize emin misiniz?',
            'Evet, Aktif Yap'
        );

        if (confirmed) {
            this.identityService.restorePermission(permission.id).subscribe({
                next: () => {
                    this.loadPermissions();
                    this.openMenuId.set(null);
                    this.toaster.success('İzin tekrar aktif edildi.');
                },
                error: (err) => {
                    console.error('Restore permission failed', err);
                    this.toaster.error('İzin aktif edilemedi');
                }
            });
        }
    }

    async permanentDeletePermission(permission: PermissionDto) {
        const confirmed = await this.toaster.confirm(
            'DİKKAT! Kalıcı Silme',
            'Bu izni kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz.',
            'Evet, Kalıcı Sil'
        );

        if (confirmed) {
            this.identityService.deletePermission(permission.id, true).subscribe({
                next: () => {
                    this.loadPermissions();
                    this.openMenuId.set(null);
                    this.toaster.success('İzin kalıcı olarak silindi.');
                },
                error: (err) => {
                    console.error('Permanent delete permission failed', err);
                    this.toaster.error('İzin silinemedi');
                }
            });
        }
    }
}
