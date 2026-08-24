import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { profileSetupGuard } from '../../core/guards/profile-setup.guard';
import { assessmentGuard, assessmentCompletedGuard } from '../../core/guards/assessment.guard';
import { subscriptionGuard } from '../../core/guards/subscription.guard';

export const studentRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./student-layout.component').then(m => m.StudentLayoutComponent),
    children: [
      {
        path: 'profile-setup',
        loadComponent: () => import('./profile-setup/profile-setup.component').then(m => m.ProfileSetupComponent)
      },
      {
        path: 'assessment-intro',
        canActivate: [authGuard, profileSetupGuard, assessmentCompletedGuard],
        loadComponent: () => import('./assessment/assessment-intro.component').then(m => m.AssessmentIntroComponent)
      },
      {
        path: 'assessment',
        canActivate: [authGuard, profileSetupGuard],
        loadComponent: () => import('./assessment/assessment.component').then(m => m.AssessmentComponent)
      },
      {
        path: 'dashboard',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./dashboard/dashboard-new.component').then(m => m.DashboardNewComponent)
      },
      {
        path: 'daily-exercises',
        canActivate: [authGuard, profileSetupGuard, assessmentGuard, subscriptionGuard],
        loadComponent: () => import('./daily-exercises/daily-exercises.component').then(m => m.DailyExercisesComponent)
      },

      {
        path: 'exercises',
        canActivate: [authGuard],
        data: { role: ['Admin', 'Teacher', 'Editor', 'Student', 'InstitutionAdmin'] },
        loadComponent: () => import('./exercises/exercises-list.component').then(m => m.ExercisesListComponent)
      },
      {
        path: 'assignments',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./assignments/student-assignments-page.component').then(m => m.StudentAssignmentsPageComponent)
      },

      {
        path: 'exercises/universal-player/:exerciseId',
        canActivate: [authGuard],
        loadComponent: () => import('./exercises/universal-player/exercise-player.component').then(m => m.ExercisePlayerComponent)
      },

      {
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent)
      },
      {
        path: 'settings',
        canActivate: [authGuard],
        loadComponent: () => import('./settings/settings.component').then(m => m.SettingsComponent)
      },
      {
        path: 'notifications',
        canActivate: [authGuard],
        loadComponent: () => import('../notifications/notifications-page.component').then(m => m.NotificationsPageComponent)
      },
      {
        path: 'notifications/preferences',
        canActivate: [authGuard],
        loadComponent: () => import('../notifications/notification-preferences.component').then(m => m.NotificationPreferencesComponent)
      },
      {
        path: 'achievements',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./achievements/achievements.component').then(m => m.AchievementsComponent)
      },

      {
        path: 'learning-path',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./learning-path/learning-path-page.component').then(m => m.LearningPathPageComponent)
      },
      {
        path: 'reviews',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./reviews/review-exercises.component').then(m => m.ReviewExercisesComponent)
      },
      {
        path: 'reports',
        canActivate: [authGuard, profileSetupGuard, subscriptionGuard],
        loadComponent: () => import('./reports/student-reports.component').then(m => m.StudentReportsComponent)
      },
      {
        path: 'coaching',
        canActivate: [authGuard, profileSetupGuard],
        loadComponent: () => import('./coaching/student-coaching.component').then(m => m.StudentCoachingComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  }
];
