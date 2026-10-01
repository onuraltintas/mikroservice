import { Routes } from '@angular/router';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';

export const IDENTITY_ROUTES: Routes = [
    {
        path: 'legal-pages',
        data: { permission: ADMIN_PERMISSIONS.privacyManage, role: 'SystemAdmin' },
        loadComponent: () => import('./pages/legal-pages').then(m => m.LegalPagesComponent)
    },
    {
        path: 'privacy-requests',
        data: { permission: ADMIN_PERMISSIONS.privacyView, role: 'SystemAdmin' },
        loadComponent: () => import('./pages/privacy-requests').then(m => m.PrivacyRequestsComponent)
    },
    {
        path: 'users',
        data: { permission: ADMIN_PERMISSIONS.usersView },
        loadComponent: () => import('./pages/user-list/user-list').then(m => m.UserListComponent)
    },
    {
        path: 'bulk-users',
        data: { permission: ADMIN_PERMISSIONS.usersView, role: 'SystemAdmin' },
        loadComponent: () => import('./pages/bulk-user-operations').then(m => m.BulkUserOperationsComponent)
    },
    {
        path: 'parent-student-relationships',
        data: { permission: ADMIN_PERMISSIONS.usersView, role: 'SystemAdmin' },
        loadComponent: () => import('./pages/parent-student-relationships').then(m => m.ParentStudentRelationshipsComponent)
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
