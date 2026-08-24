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
    const isAdmin = user?.roles?.includes('Admin');
    const isEditor = user?.roles?.includes('Editor');
    const canViewExercises = isAdmin || isEditor;

    const items: MenuItem[] = [
      {
        label: 'Ana Sayfa',
        icon: 'dashboard',
        route: '/student/dashboard'
      }
    ];

    // Only show Exercises and Reading tabs to Admin and Editor users
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

  getAdminMenuItems(): MenuItem[] {
    const user = this.authService.currentUserValue;
    const isAdmin = user?.roles?.includes('Admin');
    const isEditor = user?.roles?.includes('Editor');
    const isOnlyEditor = isEditor && !isAdmin;

    const items: MenuItem[] = [
      {
        label: 'Ana Sayfa',
        icon: 'dashboard',
        route: '/admin/dashboard'
      },
      {
        // Identity Servisi: kullanıcı, rol ve iletişim yönetimi
        label: 'Kullanıcılar',
        icon: 'people',
        children: [
          { label: 'Tüm Kullanıcılar', icon: 'group', route: '/admin/users' },
          { label: 'Öğrenciler', icon: 'school', route: '/admin/students' },
          { label: 'Öğretmenler', icon: 'person', route: '/admin/teachers' },
          { label: 'Kurumlar', icon: 'business', route: '/admin/institutions' },
          { label: 'Editörler', icon: 'edit_note', route: '/admin/editors' },
          { label: 'Roller', icon: 'security', route: '/admin/roles' },
          { label: 'Toplu İşlemler', icon: 'dynamic_feed', route: '/admin/bulk-operations' },
          { label: 'Bildirimler', icon: 'list', route: '/admin/notifications/all' },
          { label: 'Toplu Bildirim Gönder', icon: 'send', route: '/admin/notifications/send' },
          { label: 'E-posta Şablonları', icon: 'email', route: '/admin/email-templates' },
          { label: 'E-posta Kampanyaları', icon: 'campaign', route: '/admin/email-campaigns' }
        ]
      },
      {
        // Content Servisi: hızlı okuma içerik, program yönetimi ve raporlama
        label: 'Hızlı Okuma',
        icon: 'speed',
        children: [
          { label: 'Seviye Tespit Ayarları', icon: 'settings', route: '/admin/assessment-config' },
          { label: 'Program Şablonları', icon: 'playlist_play', route: '/admin/program-templates' },
          { label: 'Soru Bankası', icon: 'quiz', route: '/admin/question-bank' },
          { label: 'Egzersizler', icon: 'fitness_center', route: '/admin/exercises' },
          { label: 'Egzersiz Tipleri', icon: 'category', route: '/admin/exercise-types' },
          { label: 'Okuma Metinleri', icon: 'menu_book', route: '/admin/reading-texts' },
          { label: 'Kelime Havuzu', icon: 'translate', route: '/admin/vocabulary' },
          { label: 'Görselleştirme Sahneleri', icon: 'panorama', route: '/admin/cms/visualization-scenes' },
          { label: 'Başarımlar', icon: 'emoji_events', route: '/admin/achievements' },
          { label: 'Yaş Grupları', icon: 'groups', route: '/admin/age-groups' },
          { label: 'Öğrenci Raporları', icon: 'person', route: '/admin/reports/students' },
          { label: 'Program Analitiği', icon: 'insights', route: '/admin/analytics/programs' },
          { label: 'Kurum Raporu', icon: 'business', route: '/admin/reports/institutions' },
          { label: 'Platform Kullanımı', icon: 'monitoring', route: '/admin/reports/platform-usage' },
          { label: 'Platform Metrikleri', icon: 'speed', route: '/admin/metrics' },
          { label: 'İçerik Analizi', icon: 'inventory', route: '/admin/reports/content' },
          { label: 'Rapor Şablonları', icon: 'description', route: '/admin/report-templates' }
        ]
      },
      {
        // Coaching Servisi: öğrenci koçluğu, hedef ve sınav takibi
        label: 'Koçluk',
        icon: 'school',
        children: [
          { label: 'Koçlar', icon: 'sports', route: '/admin/coaches' },
          { label: 'Koçluk İlişkileri', icon: 'people', route: '/admin/coaching/relationships' },
          { label: 'Koçluk Seansları', icon: 'event', route: '/admin/coaching/sessions' },
          { label: 'Hedefler', icon: 'flag', route: '/admin/coaching/goals' },
          { label: 'Çalışma Oturumları', icon: 'timer', route: '/admin/coaching/study-sessions' },
          { label: 'Sınav Sonuçları', icon: 'analytics', route: '/admin/coaching/exam-results' },
          { label: 'Ödevler', icon: 'assignment', route: '/admin/coaching/assignments' },
          { label: 'Dersler & Konular', icon: 'menu_book', route: '/admin/coaching/subjects' },
          { label: 'Haftalık Özetler', icon: 'summarize', route: '/admin/coaching/snapshots' },
          { label: 'Zayıf Konu Analizi', icon: 'query_stats', route: '/admin/coaching/weak-subjects' }
        ]
      },
      {
        // Subscription Servisi: abonelik planları ve kullanıcı abonelikleri
        label: 'Abonelikler',
        icon: 'subscriptions',
        children: [
          { label: 'Planlar & Abonelikler', icon: 'workspace_premium', route: '/admin/subscriptions' }
        ]
      },
      {
        // CMS Servisi: web sitesi içerik yönetimi
        label: 'Web Sitesi',
        icon: 'web',
        children: [
          { label: 'Ana Sayfa', icon: 'home', route: '/admin/cms/ana-sayfa' },
          { label: 'Hakkımızda', icon: 'info', route: '/admin/cms/hakkimizda' },
          { label: 'Footer', icon: 'view_agenda', route: '/admin/cms/footer' },
          { label: 'Sayfalar', icon: 'pages', route: '/admin/cms/pages' },
          { label: 'Blog', icon: 'article', route: '/admin/cms/blog' },
          { label: 'Bülten Aboneleri', icon: 'mark_email_read', route: '/admin/cms/newsletter' },
          { label: 'İletişim', icon: 'contact_support', route: '/admin/cms/iletisim' }
        ]
      },
      {
        // Admin Servisi: platform yönetimi ve bakım araçları
        label: 'Sistem',
        icon: 'settings',
        children: [
          { label: 'Kurum Olarak Görün', icon: 'business', route: '/admin/impersonate/institution', badge: 'SEÇ', badgeColor: 'accent' },
          { label: 'Öğretmen Olarak Görün', icon: 'person', route: '/admin/impersonate/teacher', badge: 'SEÇ', badgeColor: 'accent' },
          { label: 'Öğrenci Modu', icon: 'science', route: '/admin/test-mode', badge: 'BETA', badgeColor: 'warn' },
          { label: 'Sistem Sağlığı', icon: 'health_and_safety', route: '/admin/reports/health' },
          { label: 'Aktivite Logları', icon: 'history', route: '/admin/audit-logs' },
          { label: 'Ayarlar', icon: 'tune', route: '/admin/settings' },
          { label: 'Yasal Dokümanlar', icon: 'gavel', route: '/admin/legal-documents' },
          { label: 'Veritabanı Yedekleri', icon: 'backup', route: '/admin/system/backups' },
          { label: 'Silinen Kayıtlar', icon: 'delete_sweep', route: '/admin/deleted-records' }
        ]
      }
    ];

    if (isOnlyEditor) {
      return items.filter(item =>
        item.label === 'Hızlı Okuma' ||
        item.label === 'Web Sitesi'
      );
    }

    return items;
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  isActive(route: string): boolean {
    return this.router.url === route || this.router.url.startsWith(route + '/');
  }
}
