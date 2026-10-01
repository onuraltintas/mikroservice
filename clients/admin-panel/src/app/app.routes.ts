import { Routes } from '@angular/router';
import { authGuard, coachingPortalGuard } from './core/auth/auth.guard';

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

    // Public Koçluk CMS and plan catalog. Coaching administration remains in the dashboard.
    {
        path: 'coaching',
        loadComponent: () => import('./features/coaching-cms/layout/coaching-public-cms-layout.component').then(m => m.CoachingPublicCmsLayoutComponent),
        children: [
            { path: '', loadComponent: () => import('./features/coaching-cms/pages/coaching-public-home.component').then(m => m.CoachingPublicHomeComponent) },
            { path: 'faq', loadComponent: () => import('./features/coaching-cms/pages/coaching-public-faq.component').then(m => m.CoachingPublicFaqComponent) },
            { path: 'pages/sikca-sorulan-sorular', redirectTo: '/coaching/faq', pathMatch: 'full' },
            { path: 'pages/:slug', loadComponent: () => import('./features/coaching-cms/pages/coaching-public-page.component').then(m => m.CoachingPublicPageComponent) },
            { path: 'blog', loadComponent: () => import('./features/coaching-cms/pages/coaching-public-blog.component').then(m => m.CoachingPublicBlogComponent) },
            { path: 'blog/:slug', loadComponent: () => import('./features/coaching-cms/pages/coaching-public-blog-post.component').then(m => m.CoachingPublicBlogPostComponent) },
            {
                path: 'newsletter',
                children: [
                    { path: '', pathMatch: 'full', loadComponent: () => import('./features/coaching-cms/components/coaching-newsletter-signup.component').then(m => m.CoachingNewsletterSignupComponent) },
                    { path: 'confirm', data: { action: 'confirm' }, loadComponent: () => import('./features/coaching-cms/components/coaching-newsletter-result.component').then(m => m.CoachingNewsletterResultComponent) },
                    { path: 'unsubscribe', data: { action: 'unsubscribe' }, loadComponent: () => import('./features/coaching-cms/components/coaching-newsletter-result.component').then(m => m.CoachingNewsletterResultComponent) }
                ]
            }
        ]
    },

    // One public legal content source shared by Coaching and Speed Reading.
    {
        path: 'legal/:slug',
        loadComponent: () => import('./features/public/legal-page.component').then(m => m.PlatformLegalPageComponent)
    },

    // Student Coaching workspace. Teacher/institution management lives in the staff portal.
    {
        path: 'coaching-portal',
        canActivate: [authGuard, coachingPortalGuard],
        loadComponent: () => import('./features/coaching-portal/layout/coaching-portal-layout.component').then(m => m.CoachingPortalLayoutComponent),
        children: [
            {
                path: '',
                loadChildren: () => import('./features/coaching-portal/coaching-portal.routes').then(m => m.COACHING_PORTAL_ROUTES)
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
