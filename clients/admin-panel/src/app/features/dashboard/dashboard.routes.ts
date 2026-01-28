import { Routes } from '@angular/router';

export const DASHBOARD_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./pages/dashboard-home/dashboard-home').then(m => m.DashboardHomeComponent),
        pathMatch: 'full'
    },
    {
        path: 'identity/profile',
        loadComponent: () => import('../identity/pages/profile-settings/profile-settings').then(m => m.ProfileSettingsComponent)
    },
    {
        path: 'identity',
        loadChildren: () => import('../identity/identity.routes').then(m => m.IDENTITY_ROUTES)
    }
];
