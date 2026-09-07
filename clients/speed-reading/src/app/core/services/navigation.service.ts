import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

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
    const user = this.authService.currentUserValue;
    const isEditor = user?.roles?.includes('Editor');
    const canViewExercises = isEditor;

    const items: MenuItem[] = [
      {
        label: 'Ana Sayfa',
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
        label: 'Günlük Egzersizler',
        icon: 'fitness_center',
        route: '/student/daily-exercises'
      },
      {
        label: 'Koçluk Panelim',
        icon: 'sports',
        route: '/student/coaching'
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

  getTeacherMenuItems(): MenuItem[] {
    return [
      {
        label: 'Ana Sayfa',
        icon: 'dashboard',
        route: '/teacher/dashboard'
      },
      {
        label: 'Öğrenciler',
        icon: 'people',
        route: '/teacher/students'
      },
      {
        label: 'Ödevler',
        icon: 'assignment',
        route: '/teacher/assignments'
      },
      {
        label: 'Koçluk',
        icon: 'sports_kabaddi',
        route: '/teacher/coaching'
      },
      {
        label: 'Sınıf Raporu',
        icon: 'analytics',
        route: '/teacher/reports/class-overview'
      },
      {
        label: 'Egzersiz Önizleme',
        icon: 'preview',
        route: '/student/exercises'
      }
    ];
  }

  getCoachingMenuItems(): MenuItem[] {
    return [
      { label: 'Genel Bakış',      icon: 'dashboard',  route: '/coaching/dashboard' },
      { label: 'Öğrencilerim',     icon: 'people',     route: '/coaching/students' },
      { label: 'Koçluk Seansları', icon: 'event',      route: '/coaching/sessions' },
      { label: 'Hedefler',         icon: 'flag',        route: '/coaching/goals' },
      { label: 'Ödevler',          icon: 'assignment', route: '/coaching/assignments' },
      { label: 'Sınav Sonuçları',  icon: 'analytics',  route: '/coaching/exam-results' },
    ];
  }

  getInstitutionAdminMenuItems(): MenuItem[] {
    return [
      {
        label: 'Ana Sayfa',
        icon: 'dashboard',
        route: '/teacher/dashboard'
      },
      {
        label: 'Öğretmenler',
        icon: 'school',
        route: '/teacher/teachers'
      },
      {
        label: 'Tüm Öğrenciler',
        icon: 'people',
        route: '/teacher/students'
      },

      {
        label: 'Raporlar',
        icon: 'analytics',
        children: [
          {
            label: 'Kurum Özeti',
            icon: 'business',
            route: '/teacher/reports/class-overview',
            queryParams: { mode: 'institution' }
          },
          {
            label: 'Öğretmen Bazlı',
            icon: 'person',
            route: '/teacher/reports/class-overview',
            queryParams: { mode: 'teacher' }
          }
        ]
      },
      {
        label: 'Kurum Ayarları',
        icon: 'settings',
        route: '/teacher/institution-settings'
      },
      {
        label: 'Egzersiz Önizleme',
        icon: 'preview',
        route: '/student/exercises'
      }
    ];
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  isActive(route: string): boolean {
    return this.router.url === route || this.router.url.startsWith(route + '/');
  }
}
