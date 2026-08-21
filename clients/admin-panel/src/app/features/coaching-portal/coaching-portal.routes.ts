import { Routes } from '@angular/router';

export const COACHING_PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/coaching-portal-home.component').then(m => m.CoachingPortalHomeComponent)
  },
  {
    path: 'assignments',
    loadComponent: () => import('./pages/student-assignments.component').then(m => m.StudentAssignmentsComponent)
  },
  {
    path: 'progress',
    loadComponent: () => import('./pages/coaching-portal-progress.component').then(m => m.CoachingPortalProgressComponent)
  },
  {
    path: 'sessions',
    loadComponent: () => import('./pages/coaching-sessions.component').then(m => m.CoachingSessionsComponent)
  },
  {
    path: 'notifications',
    loadComponent: () => import('./pages/coaching-notifications.component').then(m => m.CoachingNotificationsComponent)
  },
  {
    path: 'assignments/:id',
    loadComponent: () => import('./pages/student-assignment-detail.component').then(m => m.StudentAssignmentDetailComponent)
  },
  {
    path: 'teacher/assignments',
    loadComponent: () => import('./pages/teacher-assignments.component').then(m => m.TeacherAssignmentsComponent)
  },
  {
    path: 'children',
    loadComponent: () => import('./pages/parent-children.component').then(m => m.ParentChildrenComponent)
  }
];
