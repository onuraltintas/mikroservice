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
        path: 'coaching/assignments/new',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingManage },
        loadComponent: () => import('./pages/coaching-assignment-create').then(m => m.CoachingAssignmentCreateComponent)
    },
    {
        path: 'coaching/assignments',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingView },
        loadComponent: () => import('./pages/coaching-assignments').then(m => m.CoachingAssignmentsComponent)
    },
    {
        path: 'coaching/assignments/:id/edit',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingManage },
        loadComponent: () => import('./pages/coaching-assignment-edit').then(m => m.CoachingAssignmentEditComponent)
    },
    {
        path: 'coaching/assignments/:id',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingView },
        loadComponent: () => import('./pages/coaching-assignment-detail').then(m => m.CoachingAssignmentDetailComponent)
    },
    ...(['session', 'exam', 'goal'] as const).map(resource => ({
        path: `coaching/operations/new/${resource}`,
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingManage, resource },
        loadComponent: () => import('./pages/coaching-resource-create').then(m => m.CoachingResourceCreateComponent)
    })),
    ...(['session', 'exam', 'goal'] as const).map(resource => ({
        path: `coaching/operations/${resource}/:id/edit`,
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingManage, resource },
        loadComponent: () => import('./pages/coaching-resource-edit').then(m => m.CoachingResourceEditComponent)
    })),
    ...(['session', 'exam'] as const).map(resource => ({
        path: `coaching/operations/${resource}/:id`,
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingView, resource },
        loadComponent: () => import('./pages/coaching-resource-detail').then(m => m.CoachingResourceDetailComponent)
    })),
    {
        path: 'coaching/operations',
        canActivate: [permissionGuard],
        data: { permission: ADMIN_PERMISSIONS.coachingView },
        loadComponent: () => import('./pages/coaching-operational').then(m => m.CoachingOperationalComponent)
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
        path: 'settings/admin-audit',
        canActivate: [permissionGuard],
        loadComponent: () => import('../settings/pages/admin-audit/admin-audit.component').then(m => m.AdminAuditComponent),
        data: { title: 'Yönetici Denetim Kayıtları', permission: ADMIN_PERMISSIONS.operationsView, role: 'SystemAdmin' }
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
