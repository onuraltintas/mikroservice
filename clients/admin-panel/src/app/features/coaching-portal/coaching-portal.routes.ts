import { Routes } from '@angular/router';
import { coachingRoleGuard } from '../../core/auth/auth.guard';
import type { StudentStudyPlansComponent } from './pages/student-study-plans.component';

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
    path: 'study-plans',
    canActivate: [coachingRoleGuard],
    canDeactivate: [(component: StudentStudyPlansComponent) => component.canLeavePage()],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-study-plans.component').then(m => m.StudentStudyPlansComponent)
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
