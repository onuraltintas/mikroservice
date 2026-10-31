import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Notification, NotificationService } from '../../../core/services/notification.service';
import { groupNotificationsByDay } from '../layout/coaching-notification-panel.component';
import { parentNotificationLabel, parentNotificationTarget } from './parent-model';

/**
 * V4 (01.11.2026): the parent's Bildirimler page. The in-app weekly summary lands here and opens the child's page; the
 * list is grouped by day like the bell panel.
 */
@Component({
  selector: 'app-parent-notifications',
  standalone: true,
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cp-page" aria-labelledby="parent-notifications-title">
      <header class="cp-header cp-header-row">
        <div><h1 id="parent-notifications-title">Bildirimler</h1>
          <p>Çocuğunun haftalık özeti, öğretmen mesajları ve bağlantı haberleri burada.</p></div>
        <button type="button" class="cp-secondary" data-testid="read-all" [disabled]="!unreadCount() || busy()" (click)="readAll()">Tümünü okundu yap</button>
      </header>
      @if (error()) { <p class="cp-alert" role="alert">{{ error() }}</p> }
      @for (group of groups(); track group.label) {
        <section class="cp-section" [attr.aria-label]="group.label">
          <h2 class="cp-day">{{ group.label }}</h2>
          <ul class="cp-notices">
            @for (item of group.items; track item.id) {
              <li [class.is-unread]="!item.isRead">
                <span class="cp-dot" aria-hidden="true"></span>
                <div class="cp-notice-text">
                  <strong>{{ item.title }}</strong>
                  @if (item.message) { <p>{{ item.message }}</p> }
                  <small>{{ label(item.type) }} · {{ item.createdAt | date:'HH:mm' }}@if (!item.isRead) { · <b>Yeni</b> }</small>
                </div>
                <div class="cp-notice-actions">
                  @if (target(item); as destination) { <a class="cp-primary-link" [routerLink]="destination.path" (click)="open(item)">{{ destination.label }}</a> }
                  @else if (!item.isRead) { <button type="button" class="cp-secondary" [disabled]="busy()" (click)="markRead(item)">Okundu</button> }
                  <button type="button" class="cp-secondary" data-testid="delete-notification" [disabled]="busy()" [attr.aria-label]="item.title + ' bildirimini sil'" (click)="remove(item)">Sil</button>
                </div>
              </li>
            }
          </ul>
        </section>
      } @empty {
        <p class="cp-empty">Henüz bildirimin yok. Çocuğunun haftalık özeti ve öğretmen mesajları geldiğinde burada göreceksin.</p>
      }
    </div>`
})
export class ParentNotificationsComponent implements OnInit {
  private readonly notifications = inject(NotificationService);
  readonly unreadCount = this.notifications.unreadCount;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly groups = computed(() => groupNotificationsByDay(this.notifications.notifications()));
  readonly label = (type: string) => parentNotificationLabel(type);
  readonly target = parentNotificationTarget;

  ngOnInit(): void { this.notifications.fetchNotifications(); }

  open(item: Notification) { if (!item.isRead) this.notifications.markAsRead(item.id).subscribe({ error: () => undefined }); }
  markRead(item: Notification) { this.run(this.notifications.markAsRead(item.id), 'Bildirim okundu olarak işaretlenemedi.'); }
  readAll() { this.run(this.notifications.markAllAsRead(), 'Bildirimler okundu olarak işaretlenemedi.'); }
  remove(item: Notification) { this.run(this.notifications.deleteNotification(item.id), 'Bildirim silinemedi.'); }

  private run(request: import('rxjs').Observable<unknown>, message: string) {
    this.busy.set(true); this.error.set(null);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ error: () => this.error.set(message) });
  }
}
