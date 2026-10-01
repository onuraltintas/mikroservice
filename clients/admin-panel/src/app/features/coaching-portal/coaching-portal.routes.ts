import { Routes } from '@angular/router';
import { coachingRoleGuard } from '../../core/auth/auth.guard';

export const COACHING_PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/coaching-portal-home.component').then(m => m.CoachingPortalHomeComponent)
  },
  {
    path: 'teacher',
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./pages/legacy-teacher-redirect.component').then(m => m.LegacyTeacherRedirectComponent) },
      { path: '**', loadComponent: () => import('./pages/legacy-teacher-redirect.component').then(m => m.LegacyTeacherRedirectComponent) }
    ]
  },
  {
    path: 'assignments',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-assignments.component').then(m => m.StudentAssignmentsComponent)
  },
  {
    path: 'progress',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/coaching-portal-progress.component').then(m => m.CoachingPortalProgressComponent)
  },
  {
    path: 'subscription',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/coaching-subscription.component').then(m => m.CoachingSubscriptionComponent)
  },
  {
    path: 'sessions',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/coaching-sessions.component').then(m => m.CoachingSessionsComponent)
  },
  {
    path: 'notifications',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/coaching-notifications.component').then(m => m.CoachingNotificationsComponent)
  },
  {
    path: 'assignments/:id',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-assignment-detail.component').then(m => m.StudentAssignmentDetailComponent)
  }
];
