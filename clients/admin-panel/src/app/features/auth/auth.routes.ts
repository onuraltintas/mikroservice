import { Routes } from '@angular/router';

export const AUTH_ROUTES: Routes = [
    {
        path: 'support',
        loadComponent: () => import('./support/support.component').then(m => m.SupportComponent)
    },
    {
        path: '',
        redirectTo: '/auth/login',
        pathMatch: 'full'
    },
    {
        path: 'login',
        loadComponent: () => import('./login/login.component').then(m => m.LoginComponent)
    },
    {
        path: 'register',
        loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent)
    },
    {
        path: 'register/student',
        loadComponent: () => import('./register/student/student-register.component').then(m => m.StudentRegisterComponent)
    },
    {
        path: 'register/teacher',
        loadComponent: () => import('./register/teacher/teacher-register.component').then(m => m.TeacherRegisterComponent)
    },
    {
        path: 'register/institution',
        loadComponent: () => import('./register/institution/institution-register.component').then(m => m.InstitutionRegisterComponent)
    },
    {
        path: 'register/parent',
        loadComponent: () => import('./register/parent/parent-register.component').then(m => m.ParentRegisterComponent)
    },
    {
        path: 'confirm-email',
        loadComponent: () => import('./confirm-email/confirm-email.component').then(m => m.ConfirmEmailComponent)
    },
    {
        path: 'forgot-password',
        loadComponent: () => import('./forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
    },
    {
        path: 'reset-password',
        loadComponent: () => import('./reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
    }
];
