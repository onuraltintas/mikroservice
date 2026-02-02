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
    },
    {
        path: 'notifications',
        loadChildren: () => import('../notifications/notifications.routes').then(m => m.NOTIFICATION_ROUTES)
    },
    {
        path: 'settings/logs',
        loadComponent: () => import('../settings/pages/logs/logs.component').then(m => m.SystemLogsComponent),
        data: { title: 'System Logs' }
    },
    {
        path: 'settings/log-retention',
        loadComponent: () => import('../settings/pages/log-retention/log-retention.component').then(m => m.LogRetentionComponent),
        data: { title: 'Log Saklama Ayarları' }
    }
];
