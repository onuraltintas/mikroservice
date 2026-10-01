import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { StaffProduct } from '../auth/staff-auth.models';
import { SpeedReadingNotificationPreference, StaffNotification, StaffNotificationsService } from './staff-notifications.service';

type NotificationFilter = 'all' | 'unread' | 'read';

@Component({
  selector: 'staff-notification-center',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './staff-notification-center.component.html',
  styleUrl: './staff-notification-center.component.scss'
})
export class StaffNotificationCenterComponent implements OnChanges, OnInit {
  @Input({ required: true }) product: StaffProduct = 'coaching';

  private readonly service = inject(StaffNotificationsService);
  readonly notifications = signal<StaffNotification[]>([]);
  readonly preferences = signal<SpeedReadingNotificationPreference[]>([]);
  readonly isLoading = signal(false);
  readonly isUpdating = signal(false);
  readonly isSavingPreferences = signal(false);
  readonly preferencesVisible = signal(false);
  readonly filter = signal<NotificationFilter>('all');
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly pendingDeleteId = signal<string | null>(null);
  private loadedProduct: StaffProduct | null = null;

  get unreadCount(): number {
    return this.notifications().filter(notification => !notification.isRead).length;
  }

  get visibleNotifications(): StaffNotification[] {
    const filter = this.filter();
    return this.notifications().filter(notification =>
      filter === 'all' || (filter === 'unread' ? !notification.isRead : notification.isRead)
    );
  }

  ngOnChanges(): void {
    if (this.loadedProduct !== this.product) this.loadNotifications();
  }

  ngOnInit(): void {
    if (this.loadedProduct !== this.product) this.loadNotifications();
  }

  loadNotifications(): void {
    this.loadedProduct = this.product;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.service.getNotifications(this.product).pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: notifications => this.notifications.set(notifications),
      error: () => this.errorMessage.set('Bildirimler yüklenemedi. Lütfen yeniden deneyin.')
    });
  }

  markAsRead(notification: StaffNotification): void {
    if (notification.isRead) return;
    this.runUpdate(this.service.markAsRead(this.product, notification.id), () => {
      this.notifications.update(items => items.map(item => item.id === notification.id ? { ...item, isRead: true } : item));
    }, 'Bildirim okundu olarak işaretlendi.');
  }

  markAllAsRead(): void {
    if (this.unreadCount === 0) return;
    this.runUpdate(this.service.markAllAsRead(this.product), () => {
      this.notifications.update(items => items.map(item => ({ ...item, isRead: true })));
    }, 'Tüm bildirimler okundu olarak işaretlendi.');
  }

  confirmDelete(id: string): void {
    this.pendingDeleteId.set(id);
  }

  cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  deleteNotification(id: string): void {
    this.runUpdate(this.service.deleteNotification(this.product, id), () => {
      this.notifications.update(items => items.filter(item => item.id !== id));
      this.pendingDeleteId.set(null);
    }, 'Bildirim silindi.');
  }

  togglePreferences(): void {
    if (this.product !== 'speed-reading') return;
    if (this.preferences().length > 0) {
      this.preferencesVisible.update(value => !value);
      return;
    }
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.service.getSpeedReadingPreferences().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: preferences => {
        this.preferences.set(preferences);
        this.preferencesVisible.set(true);
      },
      error: () => this.errorMessage.set('Bildirim tercihleri yüklenemedi. Lütfen yeniden deneyin.')
    });
  }

  savePreferences(): void {
    this.isSavingPreferences.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.service.saveSpeedReadingPreferences(this.preferences())
      .pipe(finalize(() => this.isSavingPreferences.set(false)))
      .subscribe({
        next: () => this.successMessage.set('Bildirim tercihleri kaydedildi.'),
        error: () => this.errorMessage.set('Bildirim tercihleri kaydedilemedi. Lütfen yeniden deneyin.')
      });
  }

  setAllInApp(enabled: boolean): void {
    this.preferences.update(items => items.map(item => ({ ...item, enableInApp: enabled })));
  }

  setAllEmail(enabled: boolean): void {
    this.preferences.update(items => items.map(item => ({ ...item, enableEmail: enabled })));
  }

  typeLabel(type: number): string {
    const labels: Record<number, string> = {
      1: 'Yeni ödev', 2: 'Yaklaşan ödev', 3: 'Gecikmiş ödev', 4: 'Egzersiz tamamlandı',
      5: 'Kilometre taşı', 6: 'Haftalık ilerleme', 7: 'Aylık ilerleme', 8: 'Günlük hatırlatma',
      9: 'Sistem duyurusu', 10: 'Öğretmen geri bildirimi', 11: 'Başarı kazanıldı', 12: 'Hedef tamamlandı',
      13: 'Öğrenci etkinliği', 14: 'Program tamamlandı', 15: 'Yeni kullanıcı', 16: 'Sistem bildirimi'
    };
    return labels[type] ?? `Bildirim türü ${type}`;
  }

  private runUpdate(request: ReturnType<StaffNotificationsService['markAsRead']>, onSuccess: () => void, message: string): void {
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    request.pipe(finalize(() => this.isUpdating.set(false))).subscribe({
      next: () => {
        onSuccess();
        this.successMessage.set(message);
      },
      error: () => this.errorMessage.set('İşlem tamamlanamadı. Lütfen yeniden deneyin.')
    });
  }
}
