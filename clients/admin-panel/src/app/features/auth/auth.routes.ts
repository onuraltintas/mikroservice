import { Routes } from '@angular/router';

export const AUTH_ROUTES: Routes = [
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
    }
];
