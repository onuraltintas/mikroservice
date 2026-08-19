import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';

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
        canActivateChild: [permissionGuard],
        loadChildren: () => import('../identity/identity.routes').then(m => m.IDENTITY_ROUTES)
    },
    {
        path: 'coaching',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingView },
        loadComponent: () => import('./pages/coaching-overview').then(m => m.CoachingOverviewComponent)
    },
    {
        path: 'notifications',
        loadChildren: () => import('../notifications/notifications.routes').then(m => m.NOTIFICATION_ROUTES)
    },
    {
        path: 'settings/logs',
        canActivate: [permissionGuard],
        loadComponent: () => import('../settings/pages/logs/logs.component').then(m => m.SystemLogsComponent),
        data: { title: 'System Logs', permission: ADMIN_PERMISSIONS.operationsView }
    },
    {
        path: 'settings/log-retention',
        canActivate: [permissionGuard],
        loadComponent: () => import('../settings/pages/log-retention/log-retention.component').then(m => m.LogRetentionComponent),
        data: { title: 'Log Saklama Ayarları', permission: ADMIN_PERMISSIONS.operationsView }
    },
    {
        path: 'settings/configurations',
        canActivate: [permissionGuard],
        loadComponent: () => import('../settings/pages/configurations/configurations.component').then(m => m.ConfigurationsComponent),
        data: { title: 'Sistem Ayarları', permission: ADMIN_PERMISSIONS.operationsView }
    }
];
