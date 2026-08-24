import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTabsModule } from '@angular/material/tabs';
import {
  NotificationService,
  Notification,
  NotificationType
} from '../../core/services/notification.service';
import { ToasterService } from '../../core/services/toaster.service';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTabsModule
  ],
  templateUrl: './notifications-page.component.html',
  styleUrls: ['./notifications-page.component.scss']
})
export class NotificationsPageComponent implements OnInit, OnDestroy {
  private notificationService = inject(NotificationService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private destroy$ = new Subject<void>();

  allNotifications: Notification[] = [];
  unreadNotifications: Notification[] = [];
  readNotifications: Notification[] = [];
  loading = true;
  selectedTab = 0;

  ngOnInit(): void {
    this.loadNotifications();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadNotifications(): void {
    this.loading = true;

    // Load all notifications
    this.notificationService.getNotifications(undefined, 1, 100)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (notifications) => {
          this.allNotifications = notifications;
          this.unreadNotifications = notifications.filter(n => !n.isRead);
          this.readNotifications = notifications.filter(n => n.isRead);
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading notifications:', err);
          this.loading = false;
        }
      });
  }

  onTabChange(index: number): void {
    this.selectedTab = index;
  }

  markAsRead(notification: Notification, event: Event): void {
    event.stopPropagation();
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          this.loadNotifications();
        });
    }
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead()
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.loadNotifications();
      });
  }

  async deleteNotification(notification: Notification, event: Event): Promise<void> {
    event.stopPropagation();
    const confirmed = await this.toaster.confirm('Bu bildirimi silmek istediğinizden emin misiniz?');
    if (confirmed) {
      this.notificationService.deleteNotification(notification.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          this.loadNotifications();
        });
    }
  }

  onNotificationClick(notification: Notification): void {
    // Mark as read
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe(() => {
        this.loadNotifications();
      });
    }

    // Navigate if action URL exists
    if (notification.actionUrl) {
      this.router.navigateByUrl(notification.actionUrl);
    }
  }

  getNotificationIcon(notification: Notification): string {
    return this.notificationService.getNotificationIcon(notification.type);
  }

  getNotificationTypeLabel(type: NotificationType): string {
    return this.notificationService.getNotificationTypeLabel(type);
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
    return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
  }

  getCurrentList(): Notification[] {
    switch (this.selectedTab) {
      case 0: return this.allNotifications;
      case 1: return this.unreadNotifications;
      case 2: return this.readNotifications;
      default: return this.allNotifications;
    }
  }
}
