import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.authRoutes)
  },
  {
    path: 'student',
    canActivate: [authGuard],
    data: { role: ['Student', 'Editor', 'Teacher', 'InstitutionAdmin', 'Admin'] },
    loadChildren: () => import('./features/student/student.routes').then(m => m.studentRoutes)
  },
  {
    path: 'teacher',
    canActivate: [authGuard],
    data: { role: ['Teacher', 'InstitutionAdmin', 'Admin'] },
    loadChildren: () => import('./features/teacher/teacher.routes').then(m => m.teacherRoutes)
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    data: { role: ['Admin', 'Editor'] },
    loadChildren: () => import('./features/admin/admin.routes').then(m => m.adminRoutes)
  },
  {
    path: 'coaching',
    canActivate: [authGuard],
    data: { role: ['Coach', 'Admin'] },
    loadChildren: () => import('./features/coaching/coaching.routes').then(m => m.coachingRoutes)
  },
  {
    path: 'legal',
    children: [
      {
        path: 'terms',
        loadComponent: () => import('./features/legal/terms/terms.component').then(m => m.TermsComponent)
      },
      {
        path: 'privacy',
        loadComponent: () => import('./features/legal/privacy/privacy.component').then(m => m.PrivacyComponent)
      },
      {
        path: 'kvkk',
        loadComponent: () => import('./features/legal/kvkk/kvkk.component').then(m => m.KvkkComponent)
      },
      {
        path: 'cookies',
        loadComponent: () => import('./features/legal/cookies/cookies.component').then(m => m.CookiesComponent)
      }
    ]
  },
  {
    path: 'error/404',
    loadComponent: () => import('./features/error/not-found.component').then(m => m.NotFoundComponent)
  },
  {
    path: 'error/403',
    loadComponent: () => import('./features/error/forbidden.component').then(m => m.ForbiddenComponent)
  },
  {
    path: 'error/500',
    loadComponent: () => import('./features/error/server-error.component').then(m => m.ServerErrorComponent)
  },
  {
    path: 'no-access',
    loadComponent: () => import('./features/error/no-subscription.component').then(m => m.NoSubscriptionComponent)
  },
  {
    path: 'newsletter',
    children: [
      {
        path: 'unsubscribe',
        loadComponent: () => import('./features/newsletter/unsubscribe/unsubscribe.component').then(m => m.UnsubscribeComponent)
      }
    ]
  },
  // Public routes — catch-all :slug içeriyor, en sona alındı
  {
    path: '',
    loadChildren: () => import('./features/public/public.routes').then(m => m.publicRoutes)
  }
];
