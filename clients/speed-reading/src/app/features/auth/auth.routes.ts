import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const authRoutes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'register-school',
    loadComponent: () => import('./register-school/register-school.component').then(m => m.RegisterSchoolComponent)
  },
  {
    path: 'register-teacher',
    loadComponent: () => import('./register-teacher/register-teacher.component').then(m => m.RegisterTeacherComponent)
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'verify-email',
    loadComponent: () => import('./verify-email/verify-email.component').then(m => m.VerifyEmailComponent)
  },
  {
    path: 'accept-invitation',
    canActivate: [authGuard],
    loadComponent: () => import('./accept-invitation/accept-invitation.component').then(m => m.AcceptInvitationComponent)
  },
  {
    path: 'google-callback',
    loadComponent: () => import('./google-callback/google-callback.component').then(m => m.GoogleCallbackComponent)
  },
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  }
];
