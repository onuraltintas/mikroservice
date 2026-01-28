import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
    // Auth Routes
    {
        path: 'auth',
        loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
    },

    // Dashboard Routes (Protected by AuthGuard)
    {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/layout/dashboard-layout/dashboard-layout').then(m => m.DashboardLayoutComponent),
        children: [
            {
                path: '',
                loadChildren: () => import('./features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES)
            }
        ]
    },

    // Default Redirect
    {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
    },

    // 404 Redirect
    {
        path: '**',
        redirectTo: 'dashboard'
    }
];
