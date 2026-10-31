import { Route, Routes } from '@angular/router';
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

const student = { coachingRoles: ['Student'] };
const hub = (name: string) => () => import('./layout/student-hub.component').then(m => m.StudentHubComponent);

/**
 * T2 (31.10.2026): the old student addresses lead to their new place in the six-item menu. String redirects keep the
 * query string, and `:feeId` is carried over, so e-mail, notification and bookmark links keep working.
 */
export const LEGACY_STUDENT_REDIRECTS: Route[] = [
  ['study-plans', 'plan'],
  ['automatic-plan', 'plan/wizard'],
  ['study-plan-assistant', 'plan/wizard'],
  ['weekly-plan', 'plan/weekly'],
  ['study-availability', 'plan/hours'],
  ['weekly-plan-preview', 'plan/preview'],
  ['goals', 'growth/goals'],
  ['progress', 'growth/week'],
  ['exam-results', 'growth/exams'],
  ['setup', 'account/settings'],
  ['profile', 'account/settings/profile'],
  ['settings', 'account/settings/general'],
  ['subscription', 'account/payments'],
  ['finance', 'account/payments/fees'],
  ['finance/:feeId', 'account/payments/fees/:feeId']
].map(([path, to]) => ({
  path, pathMatch: 'full' as const,
  // An absolute string redirect drops the query string; this keeps it (plan, task, week, decision, required…) and the fragment.
  redirectTo: ({ params, queryParams, fragment }) => inject(Router).createUrlTree(
    ['/coaching-portal', ...to.split('/').map(part => part.startsWith(':') ? params[part.slice(1)] : part)],
    { queryParams, fragment: fragment ?? undefined })
}));

export const COACHING_PORTAL_ROUTES: Routes = [
  ...LEGACY_STUDENT_REDIRECTS,
  {
    path: '',
    pathMatch: 'full',
    canActivate: [parentHomeRedirect],
    loadComponent: () => import('./pages/coaching-portal-home.component').then(m => m.CoachingPortalHomeComponent)
  },
  // Planım: the plan itself, the weekly routine (lessons and free hours) and the plan wizard.
  {
    path: 'plan', canActivate: [coachingRoleGuard], data: { ...student, hub: 'plan' }, loadComponent: hub('plan'),
    children: [
      {
        path: '', canDeactivate: [async (component: StudentStudyPlansComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-study-plans.component').then(m => m.StudentStudyPlansComponent)
      },
      {
        path: 'weekly', canDeactivate: [async (component: StudentStudyRoutineComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-study-routine.component').then(m => m.StudentStudyRoutineComponent)
      },
      {
        path: 'hours', data: { initialTab: 'hours' },
        canDeactivate: [async (component: StudentStudyRoutineComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-study-routine.component').then(m => m.StudentStudyRoutineComponent)
      },
      {
        path: 'wizard', canDeactivate: [async (component: StudentStudyPlanAssistantComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-study-plan-assistant.component').then(m => m.StudentStudyPlanAssistantComponent)
      },
      {
        path: 'preview',
        canDeactivate: [async (component: import('./pages/student-weekly-outcome-plan.component').StudentWeeklyOutcomePlanComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-weekly-outcome-plan.component').then(m => m.StudentWeeklyOutcomePlanComponent)
      }
    ]
  },
  // Gelişimim: practice exams, the goal, and the weekly progress report.
  {
    path: 'growth', canActivate: [coachingRoleGuard], data: { ...student, hub: 'growth' }, loadComponent: hub('growth'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'exams' },
      {
        path: 'exams', canDeactivate: [async (component: StudentExamResultsComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-exam-results.component').then(m => m.StudentExamResultsComponent)
      },
      {
        path: 'goals',
        canDeactivate: [(component: import('./pages/student-primary-goal.component').StudentPrimaryGoalComponent) => component.canLeavePage()],
        loadComponent: () => import('./pages/student-primary-goal.component').then(m => m.StudentPrimaryGoalComponent)
      },
      {
        path: 'week', canDeactivate: [async (component: CoachingPortalProgressComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/coaching-portal-progress.component').then(m => m.CoachingPortalProgressComponent)
      }
    ]
  },
  {
    path: 'assignments', canActivate: [coachingRoleGuard], data: student,
    loadComponent: () => import('./pages/student-assignments.component').then(m => m.StudentAssignmentsComponent)
  },
  {
    path: 'assignments/:id', canActivate: [coachingRoleGuard], data: student,
    loadComponent: () => import('./pages/student-assignment-detail.component').then(m => m.StudentAssignmentDetailComponent)
  },
  // Roadmap O3: messages with teachers, for students and parents.
  {
    path: 'messages', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student', 'Parent'] },
    loadComponent: () => import('./pages/coaching-messages-page.component').then(m => m.CoachingMessagesPageComponent)
  },
  {
    path: 'messages/:conversationId', canActivate: [coachingRoleGuard], data: { coachingRoles: ['Student', 'Parent'] },
    loadComponent: () => import('./pages/coaching-messages-page.component').then(m => m.CoachingMessagesPageComponent)
  },
  {
    path: 'sessions', canActivate: [coachingRoleGuard], data: student,
    loadComponent: () => import('./pages/coaching-sessions.component').then(m => m.CoachingSessionsComponent)
  },
  {
    path: 'notifications', canActivate: [coachingRoleGuard], data: student,
    loadComponent: () => import('./pages/coaching-notifications.component').then(m => m.CoachingNotificationsComponent)
  },
  // Account menu: Profil ve ayarlar (preferences, profile, settings, theme), Ödemeler, Kişisel verilerim.
  {
    path: 'account/settings', canActivate: [coachingRoleGuard], data: { ...student, hub: 'settings' }, loadComponent: hub('settings'),
    children: [
      {
        path: '',
        canDeactivate: [async (component: import('./pages/student-learning-setup.component').StudentLearningSetupComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-learning-setup.component').then(m => m.StudentLearningSetupComponent)
      },
      {
        path: 'profile',
        canDeactivate: [async (component: import('./pages/student-coaching-profile.component').StudentCoachingProfileComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-coaching-profile.component').then(m => m.StudentCoachingProfileComponent)
      },
      {
        path: 'general',
        canDeactivate: [async (component: import('./pages/student-settings.component').StudentSettingsComponent) => await component.canLeavePage()],
        loadComponent: () => import('./pages/student-settings.component').then(m => m.StudentSettingsComponent)
      },
      { path: 'theme', loadComponent: () => import('./layout/student-theme-setting.component').then(m => m.StudentThemeSettingComponent) }
    ]
  },
  {
    path: 'account/payments', canActivate: [coachingRoleGuard], data: { ...student, hub: 'payments' }, loadComponent: hub('payments'),
    children: [
      { path: '', loadComponent: () => import('./pages/coaching-subscription.component').then(m => m.CoachingSubscriptionComponent) },
      { path: 'fees', loadComponent: () => import('./pages/coaching-finance.component').then(m => m.CoachingFinanceComponent) },
      // Roadmap O12: one fee with its instalments and payments.
      { path: 'fees/:feeId', loadComponent: () => import('./pages/student-fee-detail.component').then(m => m.StudentFeeDetailComponent) }
    ]
  },
  {
    path: 'account/data', canActivate: [coachingRoleGuard], data: student,
    loadComponent: () => import('./layout/student-personal-data.component').then(m => m.StudentPersonalDataComponent)
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
  }
];
