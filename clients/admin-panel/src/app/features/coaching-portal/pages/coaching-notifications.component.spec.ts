import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Notification } from '../../../core/services/notification.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingNotificationsComponent } from './coaching-notifications.component';

describe('CoachingNotificationsComponent', () => {
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
