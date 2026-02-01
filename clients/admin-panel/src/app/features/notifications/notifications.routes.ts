import { Routes } from '@angular/router';

export const NOTIFICATION_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./pages/notification-list/notification-list').then(m => m.NotificationListComponent)
    }
];
