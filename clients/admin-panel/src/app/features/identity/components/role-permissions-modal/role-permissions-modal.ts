import { Component, EventEmitter, Input, Output, inject, signal, computed, OnInit, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IdentityService, RoleDto, PermissionDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

interface PermissionGroup {
    name: string;
    permissions: {
        key: string;
        description: string;
        isSystem: boolean;
        selected: boolean;
    }[];
}

@Component({
    selector: 'app-role-permissions-modal',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './role-permissions-modal.html'
})
export class RolePermissionsModalComponent implements OnInit {
    @Input({ required: true }) role!: RoleDto;
    @Output() close = new EventEmitter<boolean>();

    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);
    private platformId = inject(PLATFORM_ID);

    loading = signal(true);
    saving = signal(false);
    searchTerm = signal('');

    allPermissions = signal<PermissionDto[]>([]);
    assignedPermissions = signal<Set<string>>(new Set());

    permissionGroups = computed<PermissionGroup[]>(() => {
        const perms = this.allPermissions();
        const assigned = this.assignedPermissions();
        const search = this.searchTerm().toLowerCase();

        // Group permissions by their group property
        const groupMap = new Map<string, PermissionGroup>();

        perms.forEach(p => {
            // Filter by search term
            if (search && !p.key.toLowerCase().includes(search) &&
                !p.description.toLowerCase().includes(search) &&
                !p.group.toLowerCase().includes(search)) {
                return;
            }

            // Skip deleted permissions
            if (p.isDeleted) return;

            if (!groupMap.has(p.group)) {
                groupMap.set(p.group, {
                    name: p.group,
                    permissions: []
                });
            }

            groupMap.get(p.group)!.permissions.push({
                key: p.key,
                description: p.description,
                isSystem: p.isSystem,
                selected: assigned.has(p.key)
            });
        });

        // Sort groups and permissions
        return Array.from(groupMap.values())
            .sort((a, b) => a.name.localeCompare(b.name))
            .map(g => ({
                ...g,
                permissions: g.permissions.sort((a, b) => a.key.localeCompare(b.key))
            }));
    });

    selectedCount = computed(() => this.assignedPermissions().size);
    totalCount = computed(() => this.allPermissions().filter(p => !p.isDeleted).length);

    ngOnInit() {
        if (isPlatformBrowser(this.platformId)) {
            this.loadData();
        }
    }

    async loadData() {
        this.loading.set(true);
        try {
            // Load all permissions and role's assigned permissions in parallel
            const [permissions, rolePermissions] = await Promise.all([
                this.identityService.getPermissions().toPromise(),
                this.identityService.getRolePermissions(this.role.id).toPromise()
            ]);

            this.allPermissions.set(permissions || []);
            this.assignedPermissions.set(new Set(rolePermissions?.assignedPermissions || []));
        } catch (error) {
            console.error('Error loading permissions:', error);
            this.toaster.error('İzinler yüklenirken hata oluştu');
        } finally {
            this.loading.set(false);
        }
    }

    togglePermission(permissionKey: string) {
        const assigned = new Set(this.assignedPermissions());
        if (assigned.has(permissionKey)) {
            assigned.delete(permissionKey);
        } else {
            assigned.add(permissionKey);
        }
        this.assignedPermissions.set(assigned);
    }

    toggleGroup(group: PermissionGroup) {
        const assigned = new Set(this.assignedPermissions());
        const allSelected = group.permissions.every(p => assigned.has(p.key));

        group.permissions.forEach(p => {
            if (allSelected) {
                assigned.delete(p.key);
            } else {
                assigned.add(p.key);
            }
        });

        this.assignedPermissions.set(assigned);
    }

    isGroupFullySelected(group: PermissionGroup): boolean {
        const assigned = this.assignedPermissions();
        return group.permissions.every(p => assigned.has(p.key));
    }

    isGroupPartiallySelected(group: PermissionGroup): boolean {
        const assigned = this.assignedPermissions();
        const selectedCount = group.permissions.filter(p => assigned.has(p.key)).length;
        return selectedCount > 0 && selectedCount < group.permissions.length;
    }

    onSearch(event: Event) {
        const target = event.target as HTMLInputElement;
        this.searchTerm.set(target.value);
    }

    async save() {
        this.saving.set(true);
        try {
            await this.identityService.updateRolePermissions(
                this.role.id,
                Array.from(this.assignedPermissions())
            ).toPromise();
            this.toaster.success('İzinler başarıyla güncellendi');
            this.close.emit(true);
        } catch (error) {
            console.error('Error saving permissions:', error);
            this.toaster.error('İzinler kaydedilirken hata oluştu');
        } finally {
            this.saving.set(false);
        }
    }

    cancel() {
        this.close.emit(false);
    }

    selectAll() {
        const assigned = new Set<string>();
        this.allPermissions().filter(p => !p.isDeleted).forEach(p => assigned.add(p.key));
        this.assignedPermissions.set(assigned);
    }

    deselectAll() {
        this.assignedPermissions.set(new Set());
    }
}
