import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-coaching-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './coaching-notifications.component.html',
  styleUrl: './coaching-notifications.component.scss'
})
export class CoachingNotificationsComponent {
  private readonly notificationService = inject(NotificationService);

  readonly notifications = this.notificationService.notifications;
  readonly unreadCount = this.notificationService.unreadCount;
  readonly isUpdating = signal(false);
  readonly errorMessage = signal<string | null>(null);

  refresh() {
    this.errorMessage.set(null);
    this.notificationService.fetchNotifications();
  }

  markAsRead(id: string) {
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.markAsRead(id).subscribe({
      error: () => this.errorMessage.set('Bildirim okunmuş olarak işaretlenemedi.'),
      complete: () => this.isUpdating.set(false)
    });
  }

  markAllAsRead() {
    if (this.unreadCount() === 0) return;
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.markAllAsRead().subscribe({
      error: () => this.errorMessage.set('Bildirimler okunmuş olarak işaretlenemedi.'),
      complete: () => this.isUpdating.set(false)
    });
  }

  deleteNotification(id: string) {
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.deleteNotification(id).subscribe({
      error: () => this.errorMessage.set('Bildirim silinemedi.'),
      complete: () => this.isUpdating.set(false)
    });
  }

  trackById(_: number, notification: { id: string }) {
    return notification.id;
  }
}
