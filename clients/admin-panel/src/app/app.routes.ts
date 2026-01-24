import { Routes } from '@angular/router';
import { MainLayoutComponent } from './layout/main-layout/main-layout';

export const routes: Routes = [
    {
        path: '',
        component: MainLayoutComponent,
        children: [
            {
                path: '',
                redirectTo: 'dashboard',
                pathMatch: 'full'
            },
            {
                path: 'dashboard',
                loadComponent: () => import('./features/dashboard/dashboard')
                    .then(m => m.DashboardComponent)
            },
            // Other feature routes will go here (Identity, Coaching, Settings)
        ]
    },

    // 404 Route
    {
        path: '**',
        redirectTo: 'dashboard'
    }
];
