import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { canUseStaffTraining } from '../guards/staff-training.guard';

export interface MenuItem {
  label: string;
  icon: string;
  route?: string;
  queryParams?: { [key: string]: any };
  children?: MenuItem[];
  badge?: string;
  badgeColor?: string;
}

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  constructor(
    private router: Router,
    private authService: AuthService
  ) { }

  getStudentMenuItems(): MenuItem[] {
    if (canUseStaffTraining(this.authService)) {
      return [
        { label: 'Eğitim programları', icon: 'school', route: '/student/training-programs' },
        { label: 'Aktif eğitimim', icon: 'fitness_center', route: '/student/daily-exercises' },
        { label: 'Egzersiz önizlemesi', icon: 'preview', route: '/student/exercises' }
      ];
    }
    const user = this.authService.currentUserValue;
    const isEditor = user?.roles?.includes('Editor');
    const canViewExercises = isEditor;

    const items: MenuItem[] = [
      {
        label: 'Ana sayfa',
        icon: 'dashboard',
        route: '/student/dashboard'
      }
    ];

    // Editors can preview the exercise catalogue from the student shell.
    if (canViewExercises) {
      items.push(
        {
          label: 'Egzersizler',
          icon: 'fitness_center',
          route: '/student/exercises'
        }
      );
    }

    items.push(
      {
        label: 'Günlük egzersizler',
        icon: 'fitness_center',
        route: '/student/daily-exercises'
      },
      {
        label: 'Ödevler',
        icon: 'assignment',
        route: '/student/assignments'
      },
      {
        label: 'Tekrar egzersizleri',
        icon: 'replay',
        route: '/student/reviews'
      },
      {
        label: 'Kişisel öğrenme yolu',
        icon: 'route',
        route: '/student/learning-path'
      },
      {
        label: 'Başarımlar',
        icon: 'emoji_events',
        route: '/student/achievements'
      },
      {
        label: 'Raporlar',
        icon: 'analytics',
        route: '/student/reports'
      },
      {
        label: 'Ayarlar',
        icon: 'settings',
        route: '/student/settings'
      }
    );

    return items;
  }

  getCoachingMenuItems(): MenuItem[] {
    return [
      { label: 'Genel bakış',      icon: 'dashboard',  route: '/coaching/dashboard' },
      { label: 'Öğrencilerim',     icon: 'people',     route: '/coaching/students' },
      { label: 'Koçluk seansları', icon: 'event',      route: '/coaching/sessions' },
      { label: 'Hedefler',         icon: 'flag',        route: '/coaching/goals' },
      { label: 'Ödevler',          icon: 'assignment', route: '/coaching/assignments' },
      { label: 'Sınav sonuçları',  icon: 'analytics',  route: '/coaching/exam-results' },
    ];
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  isActive(route: string): boolean {
    return this.router.url === route || this.router.url.startsWith(route + '/');
  }
}
