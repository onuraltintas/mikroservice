import { Routes } from '@angular/router';
import { coachingRoleGuard } from '../../core/auth/auth.guard';

export const COACHING_PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/coaching-portal-home.component').then(m => m.CoachingPortalHomeComponent)
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
    path: 'teacher/sessions/:id/edit',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-session-form.component').then(m => m.TeacherSessionFormComponent)
  },
  {
    path: 'teacher/sessions/new',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-session-form.component').then(m => m.TeacherSessionFormComponent)
  },
  {
    path: 'sessions',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student', 'Teacher'] },
    loadComponent: () => import('./pages/coaching-sessions.component').then(m => m.CoachingSessionsComponent)
  },
  {
    path: 'notifications',
    loadComponent: () => import('./pages/coaching-notifications.component').then(m => m.CoachingNotificationsComponent)
  },
  {
    path: 'assignments/:id',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Student', 'Teacher', 'Parent'] },
    loadComponent: () => import('./pages/student-assignment-detail.component').then(m => m.StudentAssignmentDetailComponent)
  },
  {
    path: 'teacher/students',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-students.component').then(m => m.TeacherStudentsComponent)
  },
  {
    path: 'teacher/academic',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-academic.component').then(m => m.TeacherAcademicComponent)
  },
  {
    path: 'teacher/assignments/new',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-assignment-form.component').then(m => m.TeacherAssignmentFormComponent)
  },
  {
    path: 'teacher/assignments/:id/edit',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-assignment-form.component').then(m => m.TeacherAssignmentFormComponent)
  },
  {
    path: 'teacher/assignments',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Teacher'] },
    loadComponent: () => import('./pages/teacher-assignments.component').then(m => m.TeacherAssignmentsComponent)
  },
  {
    path: 'children',
    canActivate: [coachingRoleGuard],
    data: { coachingRoles: ['Parent'] },
    loadComponent: () => import('./pages/parent-children.component').then(m => m.ParentChildrenComponent)
  }
];
