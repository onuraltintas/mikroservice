import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthService } from '../../../../core/auth/auth.service';
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

  // Computed property to filter menu based on role
  menuItems = computed(() => {
    const role = this.user()?.role;
    const items: any[] = [
      {
        label: 'Ana Sayfa',
        route: '/dashboard',
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="m2.25 12 8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25" /></svg>'
      }
    ];

    if (role === 'SystemAdmin') {
      items.push({
        label: 'Kullanıcı İşlemleri', // Parent Menu
        icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg>',
        children: [
          {
            label: 'Kullanıcılar',
            route: '/dashboard/identity/users',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg>'
          },
          {
            label: 'Roller',
            route: '/dashboard/identity/roles',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" /></svg>'
          },
          {
            label: 'İzinler',
            route: '/dashboard/identity/permissions',
            icon: '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>'
          }
        ]
      });
    }

    // Sanitize Main Icon
    return items.map(i => ({
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
