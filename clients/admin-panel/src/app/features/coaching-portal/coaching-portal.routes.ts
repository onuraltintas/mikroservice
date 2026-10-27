import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { coachingRoleGuard } from '../../core/auth/auth.guard';
import { AuthService, hasRole } from '../../core/auth/auth.service';

/** A parent who is not also a student starts on the parent panel. */
const parentHomeRedirect: CanActivateFn = () => {
  const profile = inject(AuthService).userProfile();
  return hasRole(profile, 'Parent') && !hasRole(profile, 'Student') ? inject(Router).createUrlTree(['/coaching-portal/parent']) : true;
};
import type { StudentStudyPlansComponent } from './pages/student-study-plans.component';
import type { StudentStudyRoutineComponent } from './pages/student-study-routine.component';
import type { CoachingPortalProgressComponent } from './pages/coaching-portal-progress.component';
import type { StudentStudyPlanAssistantComponent } from './pages/student-study-plan-assistant.component';
import type { StudentExamResultsComponent } from './pages/student-exam-results.component';

export const COACHING_PORTAL_ROUTES: Routes = [
  {
    path: 'settings', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    canDeactivate: [async (component: import('./pages/student-settings.component').StudentSettingsComponent) => await component.canLeavePage()],
    loadComponent: () => import('./pages/student-settings.component').then(m => m.StudentSettingsComponent)
  },
  {
    path: 'goals', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    canDeactivate: [(component: import('./pages/student-primary-goal.component').StudentPrimaryGoalComponent) => component.canLeavePage()],
    loadComponent: () => import('./pages/student-primary-goal.component').then(m => m.StudentPrimaryGoalComponent)
  },
  {
    path:'weekly-plan-preview',canActivate:[coachingRoleGuard],data:{coachingRoles:['Student']},
    canDeactivate:[async (component:import('./pages/student-weekly-outcome-plan.component').StudentWeeklyOutcomePlanComponent)=>await component.canLeavePage()],
    loadComponent:()=>import('./pages/student-weekly-outcome-plan.component').then(m=>m.StudentWeeklyOutcomePlanComponent)
  },
  {
    path:'weekly-plan',canActivate:[coachingRoleGuard],data:{coachingRoles:['Student']},
    canDeactivate:[async (component:StudentStudyRoutineComponent)=>await component.canLeavePage()],
    loadComponent:()=>import('./pages/student-study-routine.component').then(m=>m.StudentStudyRoutineComponent)
  },
  {
    path: 'setup', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    canDeactivate: [async (component: import('./pages/student-learning-setup.component').StudentLearningSetupComponent) => await component.canLeavePage()],
    loadComponent: () => import('./pages/student-learning-setup.component').then(m => m.StudentLearningSetupComponent)
  },
  {
    path: 'profile', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    canDeactivate: [async (component: import('./pages/student-coaching-profile.component').StudentCoachingProfileComponent) => await component.canLeavePage()],
    loadComponent: () => import('./pages/student-coaching-profile.component').then(m => m.StudentCoachingProfileComponent)
  },
  {
    path: 'finance', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/coaching-finance.component').then(m => m.CoachingFinanceComponent)
  },
  {
    path: 'exam-results', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    canDeactivate: [async (component: StudentExamResultsComponent) => await component.canLeavePage()],
    loadComponent: () => import('./pages/student-exam-results.component').then(m => m.StudentExamResultsComponent)
  },
  {
    path: 'automatic-plan',
    canActivate: [coachingRoleGuard],
    canDeactivate: [async (component: StudentStudyPlanAssistantComponent) => await component.canLeavePage()],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-study-plan-assistant.component').then(m => m.StudentStudyPlanAssistantComponent)
  },
  {
    path: 'study-plan-assistant', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-study-plan-assistant.component').then(m => m.StudentStudyPlanAssistantComponent)
  },
  {
    path: '',
    canActivate: [parentHomeRedirect],
    loadComponent: () => import('./pages/coaching-portal-home.component').then(m => m.CoachingPortalHomeComponent)
  },
  // Roadmap Y7: the parent's read-only panel.
  {
    path: 'parent', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Parent'] },
    loadComponent: () => import('./parent/parent-home.component').then(m => m.ParentHomeComponent)
  },
  {
    path: 'parent/children/:studentId', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Parent'] },
    loadComponent: () => import('./parent/parent-child.component').then(m => m.ParentChildComponent)
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
    path: 'study-availability',
    canActivate: [coachingRoleGuard],
    canDeactivate: [async (component: StudentStudyRoutineComponent) => await component.canLeavePage()],
    data: { coachingRoles: ['Student'], initialTab: 'hours' },
    loadComponent: () => import('./pages/student-study-routine.component').then(m => m.StudentStudyRoutineComponent)
  },
  {
    path: 'study-plans',
    canActivate: [coachingRoleGuard],
    canDeactivate: [async (component: StudentStudyPlansComponent) => await component.canLeavePage()],
    data: { coachingRoles: ['Student'] },
    loadComponent: () => import('./pages/student-study-plans.component').then(m => m.StudentStudyPlansComponent)
  },
  {
    path: 'progress',
    canActivate: [coachingRoleGuard],
    canDeactivate: [async (component: CoachingPortalProgressComponent) => await component.canLeavePage()],
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
