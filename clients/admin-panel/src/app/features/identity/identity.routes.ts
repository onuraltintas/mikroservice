import { Routes } from '@angular/router';

export const IDENTITY_ROUTES: Routes = [
    {
        path: 'users',
        loadComponent: () => import('./pages/user-list/user-list').then(m => m.UserListComponent)
    },
    {
        path: 'profile',
        loadComponent: () => import('./pages/profile-settings/profile-settings').then(m => m.ProfileSettingsComponent)
    },
    {
        path: 'roles',
        loadComponent: () => import('./pages/role-list/role-list').then(m => m.RoleListComponent)
    }
];
