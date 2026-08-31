import { Routes } from '@angular/router';


export const adminRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'test-mode',
        loadComponent: () => import('./test-mode/test-mode').then(m => m.TestModeComponent)
      },
      {
        path: 'program-templates',
        loadComponent: () => import('./program-templates/program-template-list.component').then(m => m.ProgramTemplateListComponent)
      },
      {
        path: 'program-templates/create',
        loadComponent: () => import('./program-templates/program-template-form.component').then(m => m.ProgramTemplateFormComponent)
      },
      {
        path: 'program-templates/:id/edit',
        loadComponent: () => import('./program-templates/program-template-form.component').then(m => m.ProgramTemplateFormComponent)
      },
      {
        path: 'program-templates/:id/view',
        loadComponent: () => import('./program-templates/program-template-view.component').then(m => m.ProgramTemplateViewComponent)
      },
      {
        path: 'analytics/programs',
        loadComponent: () => import('./program-templates/program-analytics.component').then(m => m.ProgramAnalyticsComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./users/users-list.component').then(m => m.UsersListComponent)
      },
      {
        path: 'roles',
        loadComponent: () => import('./roles/roles-list.component').then(m => m.RolesListComponent)
      },
      {
        path: 'editors',
        loadComponent: () => import('./editors/editors-list.component').then(m => m.EditorsListComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./settings/settings.component').then(m => m.SettingsComponent)
      },
      {
        path: 'bulk-operations',
        loadComponent: () => import('./bulk-operations/bulk-operations.component').then(m => m.BulkOperationsComponent)
      },
      {
        path: 'email-templates',
        loadComponent: () => import('./email-templates/email-templates.component').then(m => m.EmailTemplatesComponent)
      },
      {
        path: 'question-bank',
        loadChildren: () => import('./question-bank/question-bank-module').then(m => m.QuestionBankModule)
      },
      {
        path: 'exercises',
        loadComponent: () => import('./exercises/exercises-list.component').then(m => m.ExercisesListComponent)
      },
      {
        path: 'assessment-config',
        loadComponent: () => import('./assessment-config/assessment-config.component').then(m => m.AssessmentConfigComponent)
      },
      {
        path: 'exercise-types',
        loadComponent: () => import('./exercise-types/exercise-types-list.component').then(m => m.ExerciseTypesListComponent)
      },
      {
        path: 'reading-texts',
        loadComponent: () => import('./reading-texts/reading-texts-list.component').then(m => m.ReadingTextsListComponent)
      },
      {
        path: 'vocabulary',
        loadComponent: () => import('./vocabulary/vocabulary-list.component').then(m => m.VocabularyListComponent)
      },
      {
        path: 'metrics',
        loadComponent: () => import('./platform-metrics/platform-metrics.component').then(m => m.PlatformMetricsComponent)
      },
      {
        path: 'email-campaigns',
        loadComponent: () => import('./email-campaigns/email-campaigns-list.component').then(m => m.EmailCampaignsListComponent)
      },
      {
        path: 'institutions',
        loadComponent: () => import('./institutions/institutions-list.component').then(m => m.InstitutionsListComponent)
      },
      {
        path: 'achievements',
        loadComponent: () => import('./achievements/achievements-list.component').then(m => m.AchievementsListComponent)
      },
      {
        path: 'age-groups',
        loadComponent: () => import('./age-groups/age-groups-list.component').then(m => m.AgeGroupsListComponent)
      },
      {
        path: 'teachers',
        loadComponent: () => import('./teachers/teachers-list.component').then(m => m.TeachersListComponent)
      },
      {
        path: 'coaches',
        loadComponent: () => import('./coaches/coaches-list.component').then(m => m.CoachesListComponent)
      },
      {
        path: 'students',
        loadComponent: () => import('./students/students-list.component').then(m => m.StudentsListComponent)
      },
      {
        path: 'audit-logs',
        loadComponent: () => import('./audit-logs/audit-logs-list.component').then(m => m.AuditLogsListComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/admin-reports.component').then(m => m.AdminReportsComponent),
        children: [
          {
            path: 'pages',
            redirectTo: 'institutions',
            pathMatch: 'full'
          },
          {
            path: 'institutions',
            loadComponent: () => import('./reports/admin-institution-report.component').then(m => m.AdminInstitutionReportComponent)
          },
          {
            path: 'platform-usage',
            loadComponent: () => import('./reports/admin-platform-usage-report.component').then(m => m.AdminPlatformUsageReportComponent)
          },
          {
            path: 'content',
            loadComponent: () => import('./reports/admin-content-analysis-report.component').then(m => m.AdminContentAnalysisReportComponent)
          },
          {
            path: 'students',
            loadComponent: () => import('../teacher/reports/teacher-student-detail-report.component').then(m => m.TeacherStudentDetailReportComponent)
          },
          {
            path: 'students/:studentId',
            loadComponent: () => import('../teacher/reports/teacher-student-detail-report.component').then(m => m.TeacherStudentDetailReportComponent)
          },
          {
            path: 'health',
            loadComponent: () => import('./reports/admin-system-health-report.component').then(m => m.AdminSystemHealthReportComponent)
          }
        ]
      },
      {
        path: 'report-templates',
        loadComponent: () => import('./reports/report-templates-list.component').then(m => m.ReportTemplatesListComponent)
      },
      {
        path: 'notifications',
        redirectTo: 'notifications/all',
        pathMatch: 'full'
      },
      {
        path: 'notifications/all',
        loadComponent: () => import('./notifications/admin-all-notifications.component').then(m => m.AdminAllNotificationsComponent)
      },
      {
        path: 'notifications/send',
        loadComponent: () => import('./notifications/bulk-notification.component').then(m => m.BulkNotificationComponent)
      },
      {
        path: 'notifications/preferences',
        loadComponent: () => import('../notifications/notification-preferences.component').then(m => m.NotificationPreferencesComponent)
      },
      {
        path: 'cms',
        children: [
          {
            path: 'ana-sayfa',
            loadComponent: () => import('./cms/home-page-editor/home-page-editor').then(m => m.HomePageEditorComponent)
          },
          {
            path: 'hakkimizda',
            loadComponent: () => import('./cms/about-page-editor/about-page-editor').then(m => m.AboutPageEditorComponent)
          },
          {
            path: 'iletisim',
            loadComponent: () => import('./cms/contact-page-editor/contact-page-editor').then(m => m.ContactPageEditorComponent)
          },
          {
            path: 'newsletter',
            loadComponent: () => import('./cms/newsletter/newsletter-list.component').then(m => m.NewsletterListComponent)
          },
          {
            path: 'contact-messages',
            loadComponent: () => import('./cms/contact-messages/contact-messages-list.component').then(m => m.ContactMessagesListComponent)
          },
          {
            path: 'pages',
            loadComponent: () => import('./cms/pages/page-list.component').then(m => m.PageListComponent)
          },
          {
            path: 'pages/create',
            loadComponent: () => import('./cms/pages/page-form.component').then(m => m.PageFormComponent)
          },
          {
            path: 'pages/:id/edit',
            loadComponent: () => import('./cms/pages/page-form.component').then(m => m.PageFormComponent)
          },
          {
            path: 'blog',
            loadComponent: () => import('./cms/blog/blog-list.component').then(m => m.BlogListComponent)
          },
          {
            path: 'blog/create',
            loadComponent: () => import('./cms/blog/blog-post-form.component').then(m => m.BlogPostFormComponent)
          },
          {
            path: 'blog/:id/edit',
            loadComponent: () => import('./cms/blog/blog-post-form.component').then(m => m.BlogPostFormComponent)
          },
          {
            path: 'footer',
            loadComponent: () => import('./cms/footer-editor/footer-editor').then(m => m.FooterEditorComponent)
          },
          {
            path: 'visualization-scenes',
            loadComponent: () => import('./visualization-scenes/visualization-scenes-list.component').then(m => m.VisualizationScenesListComponent)
          }
        ]
      },
      {
        path: 'coaching/relationships',
        redirectTo: 'coaches',
        pathMatch: 'full'
      },
      {
        path: 'coaching/sessions',
        loadComponent: () => import('./coaching/coaching-sessions.component').then(m => m.CoachingSessionsComponent)
      },
      {
        path: 'coaching/goals',
        loadComponent: () => import('./coaching/coaching-goals.component').then(m => m.CoachingGoalsComponent)
      },
      {
        path: 'coaching/exam-results',
        loadComponent: () => import('./coaching/coaching-exam-results.component').then(m => m.CoachingExamResultsComponent)
      },
      {
        path: 'coaching/assignments',
        loadComponent: () => import('./coaching/coaching-assignments.component').then(m => m.CoachingAssignmentsComponent)
      },
      {
        path: 'coaching/study-sessions',
        redirectTo: 'coaching/sessions',
        pathMatch: 'full'
      },
      {
        path: 'coaching/subjects',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'coaching/snapshots',
        redirectTo: 'reports/platform-usage',
        pathMatch: 'full'
      },
      {
        path: 'coaching/weak-subjects',
        redirectTo: 'reports/students',
        pathMatch: 'full'
      },
      {
        path: 'system/backups',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'deleted-records',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'legal-documents',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'subscriptions',
        loadComponent: () => import('./subscriptions/subscriptions-page.component').then(m => m.SubscriptionsPageComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  }
];
