import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Notification, NotificationService } from '../../../core/services/notification.service';
import { coachingNotificationLabel } from '../../../../../shared/coaching-notification-target';

export interface NotificationDay { label: string; items: Notification[] }

const TURKEY = 'Europe/Istanbul';
const dayKey = (date: Date) => new Intl.DateTimeFormat('en-CA', { timeZone: TURKEY, year: 'numeric', month: '2-digit', day: '2-digit' }).format(date);

/** Groups notifications by their Türkiye day: "Bugün", "Dün", then the date ("28 Ekim Çarşamba"). */
export function groupNotificationsByDay(items: Notification[], now = new Date()): NotificationDay[] {
  const today = dayKey(now);
  const yesterday = dayKey(new Date(now.getTime() - 86_400_000));
  const groups = new Map<string, NotificationDay>();
  // T5: newest day first, newest notification first within the day, whatever order the list arrived in.
  const newestFirst = [...items].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  for (const item of newestFirst) {
    const created = new Date(item.createdAt);
    const key = dayKey(created);
    const label = key === today ? 'Bugün' : key === yesterday ? 'Dün'
      : new Intl.DateTimeFormat('tr-TR', { timeZone: TURKEY, day: 'numeric', month: 'long', weekday: 'long' }).format(created);
    if (!groups.has(key)) groups.set(key, { label, items: [] });
    groups.get(key)!.items.push(item);
  }
  return [...groups.values()];
}

/** T2 (31.10.2026): the bell opens this panel; the full list stays on the Bildirimler page. */
@Component({
  selector: 'app-coaching-notification-panel',
  standalone: true,
  imports: [RouterLink, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown.escape)': 'closed.emit()' },
  template: `<section class="panel" role="dialog" aria-labelledby="notification-panel-title">
    <header>
      <h2 id="notification-panel-title">Bildirimler</h2>
      <button type="button" class="close" aria-label="Bildirimleri kapat" (click)="closed.emit()">✕</button>
    </header>
    @if (groups().length) {
      @for (group of groups(); track group.label) {
        <h3>{{ group.label }}</h3>
        <ul>
          @for (item of group.items; track item.id) {
            <li [class.unread]="!item.isRead">
              <span class="dot" aria-hidden="true"></span>
              <span class="text"><strong>{{ item.title }}</strong><small>{{ typeLabel(item.type) }} · {{ item.createdAt | date:'HH:mm' }}</small></span>
              @if (!item.isRead) { <span class="sr-only">Okunmadı</span> }
            </li>
          }
        </ul>
      }
    } @else {
      <p class="empty">Yeni bildirimin yok. Bir şey olduğunda burada göreceksin.</p>
    }
    <a class="all" [routerLink]="allLink()" (click)="closed.emit()">Tümünü gör</a>
  </section>`,
  styles: [`.panel{position:fixed;z-index:70;top:72px;right:24px;width:min(380px,calc(100vw - 32px));max-height:min(560px,calc(100dvh - 96px));overflow:auto;padding:var(--coaching-space-4);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-surface);color:var(--ui-text);box-shadow:var(--coaching-raised-shadow)}
    header{display:flex;align-items:center;justify-content:space-between;margin-bottom:var(--coaching-space-2)}
    h2{margin:0;font-size:var(--coaching-text-lg);font-weight:800}
    .close{min-width:44px;min-height:44px;border:0;border-radius:var(--coaching-radius-control);background:var(--ui-surface-muted);color:var(--ui-text);font:inherit;cursor:pointer}
    h3{margin:var(--coaching-space-3) 0 var(--coaching-space-1);font-size:var(--coaching-text-sm);font-weight:700;color:var(--ui-text-muted)}
    ul{list-style:none;margin:0;padding:0}
    li{display:flex;align-items:flex-start;gap:var(--coaching-space-3);padding:var(--coaching-space-2) 0;border-bottom:1px solid var(--ui-border)}
    li:last-child{border-bottom:0}
    .dot{width:8px;height:8px;margin-top:8px;flex-shrink:0;border-radius:50%;background:transparent}
    li.unread .dot{background:var(--ui-accent)}
    .text{display:grid;min-width:0}
    strong{font-size:var(--coaching-text-body);font-weight:650;overflow-wrap:anywhere}
    small{color:var(--ui-text-muted);font-size:var(--coaching-text-sm)}
    .empty{margin:var(--coaching-space-4) 0;color:var(--ui-text-muted)}
    .all{display:flex;align-items:center;justify-content:center;min-height:44px;margin-top:var(--coaching-space-3);border-radius:var(--coaching-radius-control);background:var(--ui-brand-soft);color:var(--ui-brand-text);font-weight:700;text-decoration:none}
    .sr-only{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0)}
    @media (max-width:800px){.panel{top:76px;right:16px}}`]
})
export class CoachingNotificationPanelComponent {
  private readonly notifications = inject(NotificationService);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly closed = output<void>();
  /** V4: parents have their own Bildirimler page. */
  readonly allLink = input('/coaching-portal/notifications');
  readonly typeLabel = coachingNotificationLabel;
  readonly groups = computed(() => groupNotificationsByDay([...this.notifications.notifications()].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, 10)));

  constructor() {
    queueMicrotask(() => (this.host.nativeElement.querySelector('.close') as HTMLElement | null)?.focus());
  }
}
