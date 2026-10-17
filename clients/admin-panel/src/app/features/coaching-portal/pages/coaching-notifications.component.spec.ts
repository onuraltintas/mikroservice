import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { Notification } from '../../../core/services/notification.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingNotificationsComponent } from './coaching-notifications.component';

describe('CoachingNotificationsComponent', () => {
  it('renders a new study-plan change with its lesson details in the student inbox', () => {
    const notifications=signal<Notification[]>([]);
    const service={notifications:notifications.asReadonly(),unreadCount:signal(1).asReadonly(),fetchNotifications:vi.fn()};
    TestBed.configureTestingModule({imports:[CoachingNotificationsComponent],providers:[{provide:NotificationService,useValue:service}]});
    const fixture=TestBed.createComponent(CoachingNotificationsComponent);fixture.detectChanges();
    notifications.set([{...notification('updated',false),type:'StudyPlanUpdated',title:'Çalışma planın güncellendi',message:'Matematik: 06.01.2027 18:30'}]);fixture.detectChanges();
    const article=fixture.nativeElement.querySelector('article');expect(article.textContent).toContain('Çalışma planın güncellendi');expect(article.textContent).toContain('Matematik: 06.01.2027 18:30');
  });
  for (const action of ['markAsRead', 'markAllAsRead', 'deleteNotification'] as const) {
    it(`allows retrying ${action} after an API error`, () => {
      const pending = new Subject<void>();
      const service = {
        notifications: signal<Notification[]>([notification('n-1', false)]).asReadonly(),
        unreadCount: signal(1).asReadonly(), fetchNotifications: vi.fn(),
        markAsRead: vi.fn(() => pending), markAllAsRead: vi.fn(() => pending), deleteNotification: vi.fn(() => pending)
      };
      TestBed.configureTestingModule({ imports: [CoachingNotificationsComponent], providers: [{ provide: NotificationService, useValue: service }] });
      const fixture = TestBed.createComponent(CoachingNotificationsComponent);
      const component = fixture.componentInstance;
      component[action]('n-1'); fixture.detectChanges();
      expect(component.isUpdating()).toBe(true);
      pending.error(new Error('Temporary API failure')); fixture.detectChanges();
      expect(component.errorMessage()).toBeTruthy();
      expect(component.isUpdating()).toBe(false);
      expect((fixture.nativeElement.querySelector('article button') as HTMLButtonElement).disabled).toBe(false);
      const retried = new Subject<void>(); service[action].mockReturnValue(retried);
      component[action]('n-1'); retried.complete(); fixture.detectChanges();
      expect(service[action]).toHaveBeenCalledTimes(2);
      expect(component.errorMessage()).toBeNull(); expect(component.isUpdating()).toBe(false);
    });
  }
  it('delegates mark-as-read and mark-all actions to the shared notification service', () => {
    const notifications = signal<Notification[]>([notification('n-1', false), notification('n-2', true)]);
    const unreadCount = signal(1);
    const service = {
      notifications: notifications.asReadonly(),
      unreadCount: unreadCount.asReadonly(),
      fetchNotifications: vi.fn(),
      markAsRead: vi.fn(() => of(undefined)),
      markAllAsRead: vi.fn(() => of(undefined)),
      deleteNotification: vi.fn(() => of(undefined))
    };

    TestBed.configureTestingModule({
      imports: [CoachingNotificationsComponent],
      providers: [{ provide: NotificationService, useValue: service }]
    });
    const component = TestBed.createComponent(CoachingNotificationsComponent).componentInstance;

    component.markAsRead('n-1');
    component.markAllAsRead();

    expect(service.markAsRead).toHaveBeenCalledWith('n-1');
    expect(service.markAllAsRead).toHaveBeenCalledOnce();
  });

  it('does not send a mark-all request when there are no unread notifications', () => {
    const service = {
      notifications: signal<Notification[]>([]).asReadonly(),
      unreadCount: signal(0).asReadonly(),
      fetchNotifications: vi.fn(),
      markAsRead: vi.fn(() => of(undefined)),
      markAllAsRead: vi.fn(() => of(undefined)),
      deleteNotification: vi.fn(() => of(undefined))
    };

    TestBed.configureTestingModule({
      imports: [CoachingNotificationsComponent],
      providers: [{ provide: NotificationService, useValue: service }]
    });
    const component = TestBed.createComponent(CoachingNotificationsComponent).componentInstance;

    component.markAllAsRead();

    expect(service.markAllAsRead).not.toHaveBeenCalled();
  });
});

function notification(id: string, isRead: boolean): Notification {
  return {
    id,
    title: 'Test',
    message: 'Message',
    type: 'Info',
    createdAt: new Date('2030-01-01T00:00:00Z'),
    isRead
  };
}
