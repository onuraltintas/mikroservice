import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { catchError, finalize, from, map, mergeMap, of, toArray } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { coachingNotificationLabel, coachingNotificationTarget } from '../../../../../shared/coaching-notification-target';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-coaching-notifications',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './coaching-notifications.component.html',
  styleUrl: './coaching-notifications.component.scss'
})
export class CoachingNotificationsComponent {
  readonly typeLabel = coachingNotificationLabel;
  readonly target = (notification: Parameters<typeof coachingNotificationTarget>[0]) => coachingNotificationTarget(notification, 'student');
  private readonly notificationService = inject(NotificationService);

  readonly notifications = this.notificationService.notifications;
  readonly unreadCount = this.notificationService.unreadCount;
  readonly isUpdating = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly destroy = inject(DestroyRef);
  private readonly toaster = coachingToaster();
  readonly selectedIds = signal(new Set<string>());
  readonly selectedCount = computed(() => this.notifications().filter(n=>this.selectedIds().has(n.id)).length);
  readonly allSelected = computed(() => this.notifications().length > 0 && this.selectedCount() === this.notifications().length);
  readonly successMessage = signal<string|null>(null);
  private readonly pruneSelection = effect(() => {
    const visible = new Set(this.notifications().map(n=>n.id)); const selected = this.selectedIds();
    if ([...selected].some(id=>!visible.has(id))) this.selectedIds.set(new Set([...selected].filter(id=>visible.has(id))));
  });
  toggleSelection(id:string) {
    if(this.isUpdating() || !this.notifications().some(n=>n.id===id)) return;
    this.selectedIds.update(current=>{const next=new Set(current);if(next.has(id))next.delete(id);else next.add(id);return next;});
  }
  selectVisible() { if(!this.isUpdating()) this.selectedIds.set(this.allSelected()?new Set():new Set(this.notifications().map(n=>n.id))); }
  async bulkAction(kind:'delete'|'read') {
    if(this.isUpdating())return;
    const items=this.notifications().filter(n=>this.selectedIds().has(n.id)); if(!items.length)return;
    this.isUpdating.set(true);this.errorMessage.set(null);this.successMessage.set(null);
    if(kind==='delete' && !await this.toaster.confirm(`${items.length} seçili bildirim silinsin mi? Plan, ödev ve çalışma kayıtların silinmez.`)) {this.isUpdating.set(false);return;}
    from(items).pipe(mergeMap(item=>{
      const request=kind==='delete'?this.notificationService.deleteNotification(item.id):item.isRead?of(undefined):this.notificationService.markAsRead(item.id);
      return request.pipe(map(()=>({id:item.id,ok:true})),catchError(()=>of({id:item.id,ok:false})));
    },4),toArray(),takeUntilDestroyed(this.destroy),finalize(()=>this.isUpdating.set(false))).subscribe(results=>{
      const failed=results.filter(x=>!x.ok);this.selectedIds.set(new Set(failed.map(x=>x.id)));
      if(failed.length)this.errorMessage.set(`${results.length-failed.length} işlem tamamlandı; ${failed.length} bildirim işlenemedi. Başarısız seçimler korunuyor, yeniden deneyebilirsin.`);
      else this.successMessage.set(kind==='delete'?'Seçili bildirimler silindi.':'Seçili bildirimler okundu olarak işaretlendi.');
    });
  }

  refresh() {
    if(this.isUpdating())return;
    this.errorMessage.set(null);
    this.notificationService.fetchNotifications();
  }

  markAsRead(id: string) {
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.markAsRead(id).pipe(finalize(() => this.isUpdating.set(false))).subscribe({
      error: () => this.errorMessage.set('Bildirim okunmuş olarak işaretlenemedi.')
    });
  }

  markAllAsRead() {
    if (this.unreadCount() === 0) return;
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.markAllAsRead().pipe(finalize(() => this.isUpdating.set(false))).subscribe({
      error: () => this.errorMessage.set('Bildirimler okunmuş olarak işaretlenemedi.')
    });
  }

  deleteNotification(id: string) {
    this.isUpdating.set(true);
    this.errorMessage.set(null);
    this.notificationService.deleteNotification(id).pipe(finalize(() => this.isUpdating.set(false))).subscribe({
      error: () => this.errorMessage.set('Bildirim silinemedi.')
    });
  }

  trackById(_: number, notification: { id: string }) {
    return notification.id;
  }
  private readonly feedback = coachingFeedback(this.successMessage, this.errorMessage);
}
