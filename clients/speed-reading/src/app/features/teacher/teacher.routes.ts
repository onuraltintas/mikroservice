import { Routes } from '@angular/router';

export const teacherRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./teacher-layout.component').then(m => m.TeacherLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'assignments',
        loadComponent: () => import('./assignments/assignments.component').then(m => m.AssignmentsComponent)
      },
      {
        path: 'students',
        loadComponent: () => import('./students/students-list.component').then(m => m.StudentsListComponent)
      },
      {
        path: 'students/:id',
        loadComponent: () => import('./students/student-detail.component').then(m => m.StudentDetailComponent)
      },
      {
        path: 'teachers',
        loadComponent: () => import('./teachers/teachers-list.component').then(m => m.TeachersListComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/teacher-reports.component').then(m => m.TeacherReportsComponent),
        children: [
          {
            path: '',
            redirectTo: 'class-overview',
            pathMatch: 'full'
          },
          {
            path: 'institution-overview',
            redirectTo: 'class-overview?mode=institution',
            pathMatch: 'full'
          },
          {
            path: 'by-teacher',
            redirectTo: 'class-overview?mode=teacher',
            pathMatch: 'full'
          },
          {
            path: 'class-overview',
            loadComponent: () => import('./reports/teacher-class-overview-report.component').then(m => m.TeacherClassOverviewReportComponent)
          },
          {
            path: 'content-analysis',
            loadComponent: () => import('./reports/teacher-content-analysis-report.component').then(m => m.TeacherContentAnalysisReportComponent)
          },
          {
            path: 'progress',
            loadComponent: () => import('./reports/teacher-time-based-progress-report.component').then(m => m.TeacherTimeBasedProgressReportComponent)
          },
          {
            path: 'student-detail',
            loadComponent: () => import('./reports/teacher-student-detail-report.component').then(m => m.TeacherStudentDetailReportComponent)
          }
        ]
      },
      {
        path: 'coaching',
        loadComponent: () => import('./coaching/teacher-coaching-overview.component').then(m => m.TeacherCoachingOverviewComponent)
      },
      {
        path: 'institution-settings',
        loadComponent: () => import('./institution-settings.component').then(m => m.InstitutionSettingsComponent)
      },
      {
        path: 'notifications',
        loadComponent: () => import('../notifications/notifications-page.component').then(m => m.NotificationsPageComponent)
      },
      {
        path: 'notifications/preferences',
        loadComponent: () => import('../notifications/notification-preferences.component').then(m => m.NotificationPreferencesComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./profile/teacher-profile.component').then(m => m.TeacherProfileComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  }
];
