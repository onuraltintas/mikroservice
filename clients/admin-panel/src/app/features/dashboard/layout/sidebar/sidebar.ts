import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthService } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.html',
  styleUrls: ['./sidebar.scss']
})
export class SidebarComponent {
  private authService = inject(AuthService);
  private sanitizer = inject(DomSanitizer);
  private toaster = inject(ToasterService);
  layoutService = inject(LayoutService);

  user = this.authService.userProfile; // Signal

  expandedMenus = signal<string[]>([]);

  toggleSubmenu(label: string) {
    if (this.layoutService.isSidebarCollapsed()) {
      this.layoutService.toggleSidebar(); // Auto-expand sidebar if clicked while collapsed
    }

    this.expandedMenus.update(current => {
      if (current.includes(label)) {
        return current.filter(l => l !== label);
      } else {
        return [...current, label];
      }
    });
  }

  isExpanded(label: string) {
    return this.expandedMenus().includes(label);
  }

  // Menu visibility follows server-issued permission claims, not role names.
  menuItems = computed(() => {
    const permissions = this.user()?.permissions ?? [];
    const isSystemAdmin = this.user()?.roles?.includes('SystemAdmin') ?? false;
    const isManager = permissions.some(permission => permission.startsWith('Permissions.'));
    const hasSpeedReadingAccess = [
      ADMIN_PERMISSIONS.speedReadingView,
      ADMIN_PERMISSIONS.speedReadingContentManage,
      ADMIN_PERMISSIONS.speedReadingProgramManage,
      ADMIN_PERMISSIONS.speedReadingProgressView,
      ADMIN_PERMISSIONS.speedReadingReportView,
      ADMIN_PERMISSIONS.speedReadingPlatformAnalytics,
      ADMIN_PERMISSIONS.speedReadingGamificationManage,
      ADMIN_PERMISSIONS.speedReadingSettingsManage,
      ADMIN_PERMISSIONS.speedReadingCommunicationsManage
    ].some(permission => permissions.includes(permission));
    const items: any[] = [
      {
        label: 'Ana Sayfa',
        route: '/dashboard',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="m2.25 12 8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25" /></svg>'
      }
    ];

    if (isManager) {
      items.push({
        label: 'Kullanıcı İşlemleri', // Parent Menu
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg>',
        children: [
          {
            label: 'Kullanıcılar',
            route: '/dashboard/identity/users',
            permission: ADMIN_PERMISSIONS.usersView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg>'
          },
          {
            label: 'Roller',
            route: '/dashboard/identity/roles',
            permission: ADMIN_PERMISSIONS.rolesView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" /></svg>'
          },
          {
            label: 'İzinler',
            route: '/dashboard/identity/permissions',
            permission: ADMIN_PERMISSIONS.permissionView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>'
          },
          {
            label: 'Kurumlar',
            route: '/dashboard/identity/institutions',
            permission: ADMIN_PERMISSIONS.institutionsView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3 21h18M5 21V5l7-3 7 3v16M9 9h1m4 0h1m-6 4h1m4 0h1m-6 4h1m4 0h1" /></svg>'
          }
        ]
      });

      items.push({
        label: 'Destek ve Bildirim',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M20.25 8.511c.884.284 1.5 1.126 1.5 2.055v6.868c0 1.08-.846 1.98-1.924 2.067a48.102 48.102 0 0 1-3.476.25c-.658 0-1.312-.01-1.96-.028L12 21l-2.39-1.277a48.101 48.101 0 0 1-3.476-.25A2.062 2.062 0 0 1 4.21 17.434V10.566c0-.93.616-1.771 1.5-2.055" /></svg>',
        children: [
          ...(isSystemAdmin ? [{
            label: 'Yönetici Denetimi',
            route: '/dashboard/settings/admin-audit',
            permission: ADMIN_PERMISSIONS.operationsView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75m5.25-4.5v6c0 5.25-3.75 9-8.25 10.5C7.5 20.25 3.75 16.5 3.75 11.25v-6L12 2.25l8.25 3Z" /></svg>'
          }] : []),
          {
            label: 'Destek Gelen Kutusu',
            route: '/dashboard/notifications/support',
            permission: ADMIN_PERMISSIONS.supportView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 7.5h16.5M3.75 7.5A2.25 2.25 0 0 1 6 5.25h12a2.25 2.25 0 0 1 2.25 2.25m-16.5 0v9A2.25 2.25 0 0 0 6 18.75h12a2.25 2.25 0 0 0 2.25-2.25v-9" /></svg>'
          },
          {
            label: 'E-posta Şablonları',
            route: '/dashboard/notifications/email-templates',
            permission: ADMIN_PERMISSIONS.notificationTemplates,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5A2.25 2.25 0 0 1 19.5 19.5h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0-8.69 5.612a2.25 2.25 0 0 1-2.44 0L2.25 6.75" /></svg>'
          }
        ]
      });

      items.push({
        label: 'Koçluk',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6.75c-2.485 0-4.5 1.007-4.5 2.25s2.015 2.25 4.5 2.25 4.5-1.007 4.5-2.25S14.485 6.75 12 6.75Zm0 0V3.75m0 7.5v3m0 3v3m-6.75-7.5c0 1.243 3.022 2.25 6.75 2.25s6.75-1.007 6.75-2.25m-13.5 0v4.5c0 1.243 3.022 2.25 6.75 2.25s6.75-1.007 6.75-2.25v-4.5" /></svg>',
        children: [
          {
            label: 'Koçluk Özeti',
            route: '/dashboard/coaching',
            permission: ADMIN_PERMISSIONS.coachingView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 3v18h18M7.5 15l3-3 2.25 2.25 4.5-6" /></svg>'
          },
          {
            label: 'Ödevler ve teslimler',
            route: '/dashboard/coaching/assignments',
            permission: ADMIN_PERMISSIONS.coachingView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M5.25 4.5h13.5A2.25 2.25 0 0 1 21 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 17.25V6.75A2.25 2.25 0 0 1 5.25 4.5Z" /></svg>'
          },
          {
            label: 'Yeni ödev oluştur',
            route: '/dashboard/coaching/assignments/new',
            permission: ADMIN_PERMISSIONS.coachingManage,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>'
          },
          {
            label: 'Seanslar, sınavlar ve hedefler',
            route: '/dashboard/coaching/operations',
            permission: ADMIN_PERMISSIONS.coachingView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3 12h18M12 3v18m6.75-15.75L5.25 18.75" /></svg>'
          },
          {
            label: 'Yeni seans oluştur',
            route: '/dashboard/coaching/operations/new/session',
            permission: ADMIN_PERMISSIONS.coachingManage,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>'
          },
          {
            label: 'Yeni sınav oluştur',
            route: '/dashboard/coaching/operations/new/exam',
            permission: ADMIN_PERMISSIONS.coachingManage,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>'
          },
          {
            label: 'Yeni hedef oluştur',
            route: '/dashboard/coaching/operations/new/goal',
            permission: ADMIN_PERMISSIONS.coachingManage,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>'
          }
        ]
      });

      if (hasSpeedReadingAccess) {
        items.push({
          label: 'Hızlı Okuma',
          icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" /></svg>',
          children: [
            {
              label: 'Servis özeti',
              route: '/dashboard/speed-reading',
              permission: ADMIN_PERMISSIONS.speedReadingView,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 3v18h18M7.5 15l3-3 2.25 2.25 4.5-6" /></svg>'
            },
            {
              label: 'Analitik ve raporlar',
              route: '/dashboard/speed-reading/analytics',
              permission: ADMIN_PERMISSIONS.speedReadingPlatformAnalytics,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3 13.5 8.25 8.25l3.75 3.75L21 3m0 0v6m0-6h-6" /></svg>'
            },
            {
              label: 'Öğretmen analitiği',
              route: '/dashboard/speed-reading/teacher-analytics',
              permission: ADMIN_PERMISSIONS.speedReadingReportView,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.37 9.37 0 0 0 2.625-.372M15 19.128v-.004a6.75 6.75 0 0 0-6-6.704 6.75 6.75 0 0 0-6 6.704v.004M15 19.128a9.37 9.37 0 0 1-6 0M12 11.25a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" /></svg>'
            },
            {
              label: 'Öğrenci ilerlemeleri',
              route: '/dashboard/speed-reading/progress',
              permission: ADMIN_PERMISSIONS.speedReadingProgressView,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 19.5h15m-12-3v-6m4.5 6V6m4.5 10.5V3" /></svg>'
            },
            {
              label: 'Ürün ve abonelikler',
              route: '/dashboard/speed-reading/subscriptions',
              permission: ADMIN_PERMISSIONS.speedReadingContentManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 8.25h19.5M3 6h18a1.5 1.5 0 0 1 1.5 1.5v9A1.5 1.5 0 0 1 21 18H3a1.5 1.5 0 0 1-1.5-1.5v-9A1.5 1.5 0 0 1 3 6Z" /></svg>'
            },
            {
              label: 'İçerik yapılandırması',
              route: '/dashboard/speed-reading/content-configuration',
              permission: ADMIN_PERMISSIONS.speedReadingSettingsManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M10.5 6h9.75M3.75 6h.008v.008H3.75V6Zm0 6h.008v.008H3.75V12Zm0 6h.008v.008H3.75V18ZM6 6h.75M6 12h.75M6 18h.75m4.5-6h9.75m-9.75 6h9.75" /></svg>'
            },
            {
              label: 'Egzersiz ve okuma içeriği',
              route: '/dashboard/speed-reading/catalog',
              permission: ADMIN_PERMISSIONS.speedReadingContentManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 5.25A2.25 2.25 0 0 1 6.75 3h10.5A2.25 2.25 0 0 1 19.5 5.25v13.5A2.25 2.25 0 0 1 17.25 21H6.75A2.25 2.25 0 0 1 4.5 18.75V5.25Z" /><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 7.5h7.5m-7.5 3h7.5m-7.5 3h4.5" /></svg>'
            },
            {
              label: 'Programlar ve öğrenme yolları',
              route: '/dashboard/speed-reading/programs',
              permission: ADMIN_PERMISSIONS.speedReadingProgramManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5M3.75 17.25h16.5" /></svg>'
            },
            {
              label: 'Görselleştirme sahneleri',
              route: '/dashboard/speed-reading/visualization-scenes',
              permission: ADMIN_PERMISSIONS.speedReadingContentManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="m2.25 15.75 3.75-3.75 3 3 4.5-6 5.25 6.75M3.75 19.5h16.5a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H3.75A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Z" /></svg>'
            },
            {
              label: 'Soru ve kelime havuzu',
              route: '/dashboard/speed-reading/language-content',
              permission: ADMIN_PERMISSIONS.speedReadingContentManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9.879 7.5h4.242M9.879 12h4.242m-4.242 4.5h4.242M6.75 3.75h10.5A2.25 2.25 0 0 1 19.5 6v12a2.25 2.25 0 0 1-2.25 2.25H6.75A2.25 2.25 0 0 1 4.5 18V6a2.25 2.25 0 0 1 2.25-2.25Z" /></svg>'
            },
            {
              label: 'Başarılar ve rozetler',
              route: '/dashboard/speed-reading/achievements',
              permission: ADMIN_PERMISSIONS.speedReadingGamificationManage,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M16.5 3.75h-9A2.25 2.25 0 0 0 5.25 6v3.75A6.75 6.75 0 0 0 12 16.5a6.75 6.75 0 0 0 6.75-6.75V6a2.25 2.25 0 0 0-2.25-2.25Z" /><path stroke-linecap="round" stroke-linejoin="round" d="M9 20.25h6M12 16.5v3.75" /></svg>'
            },
            {
              label: 'Rapor yönetimi',
              route: '/dashboard/speed-reading/reports',
              permission: ADMIN_PERMISSIONS.speedReadingReportView,
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3.75-12H18m-2.25 3H18m-2.25 3H18M6.75 3.75h10.5A2.25 2.25 0 0 1 19.5 6v12a2.25 2.25 0 0 1-2.25 2.25H6.75A2.25 2.25 0 0 1 4.5 18V6a2.25 2.25 0 0 1 2.25-2.25Z" /></svg>'
            },
            {
              label: 'İletişim ve CMS',
              route: '/dashboard/speed-reading/communications',
              permissions: [ADMIN_PERMISSIONS.speedReadingContentManage, ADMIN_PERMISSIONS.speedReadingCommunicationsManage],
              icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 9.75h7.5m-7.5 3h4.5m-9 5.25 1.75-1.75A2.25 2.25 0 0 1 7.09 15.6h9.82a2.25 2.25 0 0 1 2.25 2.25v.4a2.25 2.25 0 0 1-2.25 2.25H7.09a2.25 2.25 0 0 1-1.59-.66L3.75 18Z" /></svg>'
            }
          ]
        });
      }

      items.push({
        label: 'Sistem Ayarları',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M10.343 3.94c.09-.542.56-.94 1.11-.94h1.093c.55 0 1.02.398 1.11.94l.149.894c.07.424.384.764.78.93.398.164.855.142 1.205-.108l.737-.527a1.125 1.125 0 0 1 1.45.12l.773.774c.39.389.44 1.002.12 1.45l-.527.737c-.25.35-.272.806-.107 1.204.165.397.505.71.93.78l.893.15c.543.09.94.56.94 1.109v1.094c0 .55-.397 1.02-.94 1.11l-.893.149c-.425.07-.765.383-.93.78-.165.398-.143.854.107 1.204l.527.738c.32.447.269 1.06-.12 1.45l-.774.773a1.125 1.125 0 0 1-1.449.12l-.738-.527c-.35-.25-.806-.272-1.203-.107-.397.165-.71.505-.781.929l-.149.894c-.09.542-.56.94-1.11.94h-1.094c-.55 0-1.019-.398-1.11-.94l-.148-.894c-.071-.424-.384-.764-.781-.93-.398-.164-.854-.142-1.204.108l-.738.527c-.447.32-1.06.269-1.45-.12l-.773-.774a1.125 1.125 0 0 1-.12-1.45l.527-.737c.25-.35.273-.806.108-1.204-.165-.397-.505-.71-.93-.78l-.894-.15c-.542-.09-.94-.56-.94-1.109v-1.094c0-.55.398-1.02.94-1.11l.894-.149c.424-.07.765-.383.93-.78.165-.398.143-.854-.107-1.204l-.527-.738a1.125 1.125 0 0 1 .12-1.45l.773-.773a1.125 1.125 0 0 1 1.45-.12l.737.527c.35.25.807.272 1.204.107.397-.165.71-.505.78-.929l.15-.894Z" /><path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" /></svg>',
        children: [
          {
            label: 'Sistem Logları',
            route: '/dashboard/settings/logs',
            permission: ADMIN_PERMISSIONS.operationsView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" /></svg>'
          },
          {
            label: 'Log Temizleme',
            route: '/dashboard/settings/log-retention',
            permission: ADMIN_PERMISSIONS.operationsView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" /></svg>'
          },
          {
            label: 'Sistem Ayarları',
            route: '/dashboard/settings/configurations',
            permission: ADMIN_PERMISSIONS.operationsView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M10.343 3.94c.09-.542.56-.94 1.11-.94h1.093c.55 0 1.02.398 1.11.94l.149.894c.07.424.384.764.78.93.398.164.855.142 1.205-.108l.737-.527a1.125 1.125 0 0 1 1.45.12l.773.774c.39.389.44 1.002.12 1.45l-.527.737c-.25.35-.272.806-.107 1.204.165.397.505.71.93.78l.893.15c.543.09.94.56.94 1.109v1.094c0 .55-.397 1.02-.94 1.11l-.893.149c-.425.07-.765.383-.93.78-.165.398-.143.854.107 1.204l.527.738c.32.447.269 1.06-.12 1.45l-.774.773a1.125 1.125 0 0 1-1.449.12l-.738-.527c-.35-.25-.806-.272-1.203-.107-.397.165-.71.505-.781.929l-.149.894c-.09.542-.56.94-1.11.94h-1.094c-.55 0-1.019-.398-1.11-.94l-.148-.894c-.071-.424-.384-.764-.781-.93-.398-.164-.854-.142-1.204.108l-.738.527c-.447.32-1.06.269-1.45-.12l-.773-.774a1.125 1.125 0 0 1-.12-1.45l.527-.737c.25-.35.273-.806.108-1.204-.165-.397-.505-.71-.93-.78l-.894-.15c-.542-.09-.94-.56-.94-1.109v-1.094c0-.55.398-1.02.94-1.11l.894-.149c.424-.07.765-.383.93-.78.165-.398.143-.854-.107-1.204l-.527-.738a1.125 1.125 0 0 1 .12-1.45l.773-.773a1.125 1.125 0 0 1 1.45-.12l.737.527c.35.25.807.272 1.204.107.397-.165.71-.505.78-.929l.15-.894Z" /><path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" /></svg>'
          }
        ]
      });
    }

    const permissionByRoute: Record<string, string> = {
      '/dashboard/identity/users': ADMIN_PERMISSIONS.usersView,
      '/dashboard/identity/roles': ADMIN_PERMISSIONS.rolesView,
      '/dashboard/identity/permissions': ADMIN_PERMISSIONS.permissionView,
      '/dashboard/identity/institutions': ADMIN_PERMISSIONS.institutionsView,
      '/dashboard/notifications/support': ADMIN_PERMISSIONS.supportView,
      '/dashboard/notifications/email-templates': ADMIN_PERMISSIONS.notificationTemplates,
      '/dashboard/coaching': ADMIN_PERMISSIONS.coachingView,
      '/dashboard/coaching/assignments': ADMIN_PERMISSIONS.coachingView,
      '/dashboard/coaching/assignments/new': ADMIN_PERMISSIONS.coachingManage,
      '/dashboard/coaching/operations': ADMIN_PERMISSIONS.coachingView,
      '/dashboard/coaching/operations/new/session': ADMIN_PERMISSIONS.coachingManage,
      '/dashboard/coaching/operations/new/exam': ADMIN_PERMISSIONS.coachingManage,
      '/dashboard/coaching/operations/new/goal': ADMIN_PERMISSIONS.coachingManage,
      '/dashboard/speed-reading': ADMIN_PERMISSIONS.speedReadingView,
      '/dashboard/speed-reading/analytics': ADMIN_PERMISSIONS.speedReadingPlatformAnalytics,
      '/dashboard/speed-reading/progress': ADMIN_PERMISSIONS.speedReadingProgressView,
      '/dashboard/speed-reading/subscriptions': ADMIN_PERMISSIONS.speedReadingContentManage,
      '/dashboard/speed-reading/content-configuration': ADMIN_PERMISSIONS.speedReadingSettingsManage,
      '/dashboard/speed-reading/visualization-scenes': ADMIN_PERMISSIONS.speedReadingContentManage,
      '/dashboard/speed-reading/language-content': ADMIN_PERMISSIONS.speedReadingContentManage,
      '/dashboard/speed-reading/reports': ADMIN_PERMISSIONS.speedReadingReportView,
      '/dashboard/speed-reading/communications': ADMIN_PERMISSIONS.speedReadingCommunicationsManage,
      '/dashboard/speed-reading/teacher-analytics': ADMIN_PERMISSIONS.speedReadingReportView,
      '/dashboard/speed-reading/catalog': ADMIN_PERMISSIONS.speedReadingContentManage,
      '/dashboard/speed-reading/programs': ADMIN_PERMISSIONS.speedReadingProgramManage,
      '/dashboard/speed-reading/achievements': ADMIN_PERMISSIONS.speedReadingGamificationManage,
      '/dashboard/settings/admin-audit': ADMIN_PERMISSIONS.operationsView,
      '/dashboard/settings/logs': ADMIN_PERMISSIONS.operationsView,
      '/dashboard/settings/log-retention': ADMIN_PERMISSIONS.operationsView,
      '/dashboard/settings/configurations': ADMIN_PERMISSIONS.operationsView
    };

    // Sanitize icons after filtering by server-issued permissions.
    return items
      .map(i => ({
        ...i,
         children: i.children?.filter((child: any) =>
           !child.route || child.permissions?.some((permission: string) => this.authService.hasPermission(permission)) || this.authService.hasPermission(permissionByRoute[child.route]))
      }))
      .filter(i => !i.children || i.children.length > 0)
      .map(i => ({
      ...i,
      iconHtml: this.sanitizer.bypassSecurityTrustHtml(i.icon),
      // Sanitize children icons if they exist
      children: i.children ? i.children.map((c: any) => ({ ...c, iconHtml: this.sanitizer.bypassSecurityTrustHtml(c.icon) })) : undefined
      }));
  });

  async logout() { // Make async
    const confirmed = await this.toaster.confirm(
      'Oturumunuzu sonlandırmak istediğinize emin misiniz?',
      { title: 'Çıkış Yap', confirmText: 'Evet, Çıkış Yap', cancelText: 'İptal' }
    );

    if (confirmed) {
      this.authService.logout();
    }
  }
}
