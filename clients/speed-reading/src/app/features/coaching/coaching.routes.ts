import { Routes } from '@angular/router';

export const coachingRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./coaching-layout.component').then(m => m.CoachingLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./dashboard/coach-dashboard.component').then(m => m.CoachDashboardComponent)
      },
      {
        path: 'students',
        loadComponent: () => import('./students/coach-students.component').then(m => m.CoachStudentsComponent)
      },
      {
        path: 'students/:id',
        loadComponent: () => import('./students/coach-student-detail.component').then(m => m.CoachStudentDetailComponent)
      },
      {
        path: 'sessions',
        loadComponent: () => import('./sessions/coach-sessions.component').then(m => m.CoachSessionsComponent)
      },
      {
        path: 'goals',
        loadComponent: () => import('./goals/coach-goals.component').then(m => m.CoachGoalsComponent)
      },
      {
        path: 'assignments',
        loadComponent: () => import('./assignments/coach-assignments.component').then(m => m.CoachAssignmentsComponent)
      },
      {
        path: 'exam-results',
        loadComponent: () => import('./exam-results/coach-exam-results.component').then(m => m.CoachExamResultsComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  }
];
