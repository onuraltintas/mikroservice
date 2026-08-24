import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil, catchError, of } from 'rxjs';
import {
  NotificationService,
  Notification,
  UnreadCount
} from '../../../core/services/notification.service';
import { PushService } from '../../../core/services/push.service';
import { ClickOutsideDirective } from '../../directives/click-outside.directive';

import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule, ClickOutsideDirective],
  templateUrl: './notification-bell.component.html',
  styleUrls: ['./notification-bell.component.scss']
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  private notificationService = inject(NotificationService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private pushService = inject(PushService);
  private destroy$ = new Subject<void>();

  unreadCount: UnreadCount = { total: 0, high: 0, urgent: 0 };
  recentNotifications: Notification[] = [];
  isDropdownOpen = false;

  ngOnInit(): void {
    // Only load if user is authenticated
    if (!this.authService.currentUserValue) {
      return;
    }

    // Subscribe to unread count
    this.notificationService.unreadCount$
      .pipe(takeUntil(this.destroy$))
      .subscribe(count => {
        this.unreadCount = count;
      });

    // Load initial data
    this.loadRecentNotifications();
    this.loadUnreadCount();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUnreadCount(): void {
    this.notificationService.getUnreadCount()
      .pipe(
        takeUntil(this.destroy$),
        catchError(error => {
          console.warn('⚠️ Unread count failed (non-critical):', error);
          return of({ total: 0, high: 0, urgent: 0 });
        })
      )
      .subscribe();
  }

  loadRecentNotifications(): void {
    this.notificationService.getNotifications(false, 1, 5)
      .pipe(
        takeUntil(this.destroy$),
        catchError(error => {
          console.warn('⚠️ Recent notifications failed (non-critical):', error);
          return of([]);
        })
      )
      .subscribe(notifications => {
        this.recentNotifications = notifications;
      });
  }

  toggleDropdown(): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    if (this.isDropdownOpen) {
      this.loadRecentNotifications();
    }
  }

  closeDropdown(): void {
    this.isDropdownOpen = false;
  }

  markAsRead(notification: Notification, event: Event): void {
    event.stopPropagation();
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          this.loadRecentNotifications();
        });
    }
  }

  onNotificationClick(notification: Notification): void {
    // Mark as read
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe();
    }

    // Navigate if action URL exists
    if (notification.actionUrl) {
      // Fix for legacy assignment notifications pointing to non-existent detail pages
      let url = notification.actionUrl;
      const assignmentRegex = /^\/(teacher|student)\/assignments\/([a-f\d\-]+)$/i;

      if (assignmentRegex.test(url)) {
        // Redirect /teacher/assignments/{id} -> /teacher/assignments
        const basePath = url.startsWith('/teacher') ? '/teacher/assignments' : '/student/assignments';
        url = basePath;
      }

      this.router.navigateByUrl(url);
    }

    this.closeDropdown();
  }

  enablePushNotifications(): void {
    this.pushService.requestSubscription();
    this.closeDropdown();
  }

  viewAllNotifications(): void {
    if (this.authService.hasRole('Admin')) {
      this.router.navigate(['/admin/notifications/all']);
    } else {
      let basePath = '/student';
      if (this.authService.hasRole('Teacher')) {
        basePath = '/teacher';
      }
      this.router.navigate([`${basePath}/notifications`]);
    }
    this.closeDropdown();
  }

  getNotificationIcon(notification: Notification): string {
    return this.notificationService.getNotificationIcon(notification.type);
  }

  getPriorityColor(notification: Notification): string {
    return this.notificationService.getPriorityColor(notification.priority);
  }

  getRelativeTime(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Şimdi';
    if (diffMins < 60) return `${diffMins} dakika önce`;
    if (diffHours < 24) return `${diffHours} saat önce`;
    if (diffDays < 7) return `${diffDays} gün önce`;
    return date.toLocaleDateString('tr-TR');
  }
}
