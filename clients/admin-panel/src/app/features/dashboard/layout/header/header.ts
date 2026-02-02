import { Component, signal, OnInit, inject, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';
import { DarkModeService } from '../../../../core/services/dark-mode.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { formatDistanceToNow } from 'date-fns';
import { tr } from 'date-fns/locale';
import { catchError, of, forkJoin } from 'rxjs';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent implements OnInit {
  private authService = inject(AuthService);
  private toaster = inject(ToasterService);
  private configService = inject(ConfigurationService);
  layoutService = inject(LayoutService);
  darkModeService = inject(DarkModeService);
  notificationService = inject(NotificationService);

  showNotificationMenu = signal(false);
  isMaintenanceMode = signal(false);

  showUserMenu = signal(false);
  user = this.authService.userProfile;

  ngOnInit() {
    this.checkMaintenanceStatus();
  }

  checkMaintenanceStatus() {
    // Check BOTH System Global Maintenance AND Identity Service Maintenance
    forkJoin({
      system: this.configService.getConfigurationValue('system.maintenancemode').pipe(catchError(() => of('false'))),
      identity: this.configService.getConfigurationValue('maintenance.identity').pipe(catchError(() => of('false')))
    }).subscribe({
      next: (results) => {
        const sysVal = results.system?.replace(/"/g, '').trim().toLowerCase() === 'true';
        const idVal = results.identity?.replace(/"/g, '').trim().toLowerCase() === 'true';
        this.isMaintenanceMode.set(sysVal || idVal);
      },
      error: () => this.isMaintenanceMode.set(false)
    });
  }

  toggleTheme() {
    this.darkModeService.toggle();
  }

  toggleUserMenu() {
    this.showUserMenu.update(v => !v);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    // Close menu when clicking outside
    const target = event.target as HTMLElement;
    if (!target.closest('.user-menu-container')) {
      this.showUserMenu.set(false);
    }
    if (!target.closest('.notification-container')) {
      this.showNotificationMenu.set(false);
    }
  }

  get unreadCount() {
    return this.notificationService.unreadCount();
  }

  get notifications() {
    return this.notificationService.notifications().slice(0, 5);
  }

  formatTime(date: Date) {
    return formatDistanceToNow(new Date(date), { addSuffix: true, locale: tr });
  }

  toggleNotificationMenu() {
    this.showNotificationMenu.update(v => !v);
  }

  markAsRead(id: string, event: Event) {
    event.stopPropagation();
    this.notificationService.markAsRead(id).subscribe(() => {
      this.notificationService.fetchNotifications();
    });
  }

  markAllAsRead() {
    this.notificationService.markAllAsRead().subscribe(() => {
      this.notificationService.fetchNotifications();
    });
  }

  async logout() {
    this.showUserMenu.set(false);
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
