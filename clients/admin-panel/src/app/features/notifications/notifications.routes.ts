import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';

export const NOTIFICATION_ROUTES: Routes = [
    {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./pages/notification-list/notification-list').then(m => m.NotificationListComponent)
    },
    {
        path: 'support',
        data: { permission: ADMIN_PERMISSIONS.supportView },
        canActivate: [permissionGuard],
        loadComponent: () => import('./pages/support-inbox').then(m => m.SupportInboxComponent)
    },
    {
        path: 'email-templates',
        data: { permission: ADMIN_PERMISSIONS.notificationTemplates },
        canActivate: [permissionGuard],
        loadComponent: () => import('./pages/email-templates').then(m => m.EmailTemplatesComponent)
    }
];
