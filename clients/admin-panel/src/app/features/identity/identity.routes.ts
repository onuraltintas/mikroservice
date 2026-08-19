import { Routes } from '@angular/router';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';

export const IDENTITY_ROUTES: Routes = [
    {
        path: 'users',
        data: { permission: ADMIN_PERMISSIONS.usersView },
        loadComponent: () => import('./pages/user-list/user-list').then(m => m.UserListComponent)
    },
    {
        path: 'institutions',
        data: { permission: ADMIN_PERMISSIONS.institutionsView },
        loadComponent: () => import('./pages/institution-list').then(m => m.InstitutionListComponent)
    },
    {
        path: 'profile',
        loadComponent: () => import('./pages/profile-settings/profile-settings').then(m => m.ProfileSettingsComponent)
    },
    {
        path: 'roles',
        data: { permission: ADMIN_PERMISSIONS.rolesView },
        loadComponent: () => import('./pages/role-list/role-list').then(m => m.RoleListComponent)
    },
    {
        path: 'permissions',
        data: { permission: ADMIN_PERMISSIONS.permissionView },
        loadComponent: () => import('./pages/permission-list/permission-list').then(m => m.PermissionListComponent)
    }
];
