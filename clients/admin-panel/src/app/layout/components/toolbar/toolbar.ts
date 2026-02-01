import { Component, EventEmitter, Output } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { Router, RouterLink } from '@angular/router';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/auth/auth.service';
import { inject } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { formatDistanceToNow } from 'date-fns';
import { tr } from 'date-fns/locale';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDividerModule,
    RouterLink,
    MatBadgeModule
  ],
  templateUrl: './toolbar.html',
  styleUrl: './toolbar.scss'
})
export class ToolbarComponent {
  @Output() menuToggle = new EventEmitter<void>();

  notificationService = inject(NotificationService);
  authService = inject(AuthService);
  router = inject(Router);

  get unreadCount() {
    return this.notificationService.unreadCount();
  }

  get notifications() {
    return this.notificationService.notifications().slice(0, 5); // Show last 5
  }

  get userProfile() {
    return this.authService.userProfile();
  }

  formatTime(date: Date) {
    return formatDistanceToNow(new Date(date), { addSuffix: true, locale: tr });
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

  onLogout() {
    this.authService.logout();
  }

  getInitial() {
    return this.userProfile?.firstName?.charAt(0) || this.userProfile?.email?.charAt(0) || 'U';
  }
}
