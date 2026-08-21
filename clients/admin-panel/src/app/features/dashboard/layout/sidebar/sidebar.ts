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
            label: 'Seanslar, sınavlar ve hedefler',
            route: '/dashboard/coaching/operations',
            permission: ADMIN_PERMISSIONS.coachingView,
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M3 12h18M12 3v18m6.75-15.75L5.25 18.75" /></svg>'
          }
        ]
      });

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
      '/dashboard/coaching/operations': ADMIN_PERMISSIONS.coachingView,
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
          !child.route || this.authService.hasPermission(permissionByRoute[child.route]))
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
      'Çıkış Yap',
      'Oturumunuzu sonlandırmak istediğinize emin misiniz?',
      'Evet, Çıkış Yap',
      'İptal'
    );

    if (confirmed) {
      this.authService.logout();
    }
  }
}
