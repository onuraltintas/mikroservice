import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { NotificationService } from '../../../core/services/notification.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingParentService } from './coaching-parent.service';
import { ParentNotificationSettingsComponent, ParentPersonalDataComponent } from './parent-account.component';
import { ParentNotificationsComponent } from './parent-notifications.component';

const child = '3f2a1d63-0b7e-4c49-9a59-6b4a7c1f2e10';
const toaster = { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };

// V4 (01.11.2026): parents read their in-app notices (the weekly summary lands here) on their own page.
describe('ParentNotificationsComponent', () => {
  function setup(items: object[]) {
    const notifications = signal<any[]>(items);
    const service = { notifications, unreadCount: signal(items.filter((x: any) => !x.isRead).length), fetchNotifications: vi.fn(),
      markAsRead: vi.fn(() => of(undefined)), markAllAsRead: vi.fn(() => of(undefined)), deleteNotification: vi.fn(() => of(undefined)) };
    TestBed.configureTestingModule({ imports: [ParentNotificationsComponent], providers: [provideRouter([]),
      { provide: NotificationService, useValue: service }, { provide: ToasterService, useValue: toaster }] });
    const fixture = TestBed.createComponent(ParentNotificationsComponent); fixture.detectChanges();
    return { fixture, service };
  }

  it('groups notices by day and opens the weekly summary on the child page', () => {
    const { fixture, service } = setup([
      { id: 'n1', title: "Elif'in haftalık özeti hazır", message: 'Bu hafta 9/14 görev.', type: 'ParentWeeklySummary', relatedEntityId: child, isRead: false, createdAt: new Date().toISOString() },
      { id: 'n2', title: 'Yeni mesaj', message: 'Mehmet Kaya yazdı.', type: 'CoachingMessage', relatedEntityId: child, isRead: true, createdAt: new Date(Date.now() - 2 * 86400000).toISOString() }]);
    const root = fixture.nativeElement as HTMLElement;
    expect(service.fetchNotifications).toHaveBeenCalled();
    expect(root.querySelector('h1')?.textContent).toContain('Bildirimler');
    expect(root.textContent).toContain('Bugün');
    expect(root.textContent).toContain('Haftalık özet');
    expect(root.querySelector(`a[href="/coaching-portal/parent/children/${child}"]`)?.textContent).toContain('Özeti aç');
    expect(root.querySelector(`a[href="/coaching-portal/messages/${child}"]`)).toBeTruthy();
  });

  it('marks everything read and deletes one notice', () => {
    const { fixture, service } = setup([{ id: 'n1', title: 'Özet', message: '', type: 'ParentWeeklySummary', relatedEntityId: child, isRead: false, createdAt: new Date().toISOString() }]);
    const root = fixture.nativeElement as HTMLElement;
    (root.querySelector('[data-testid="read-all"]') as HTMLButtonElement).click();
    expect(service.markAllAsRead).toHaveBeenCalled();
    (root.querySelector('[data-testid="delete-notification"]') as HTMLButtonElement).click();
    expect(service.deleteNotification).toHaveBeenCalledWith('n1');
  });

  it('says so in the parent voice when there is nothing', () => {
    const { fixture } = setup([]);
    expect(fixture.nativeElement.textContent).toContain('Henüz bildirimin yok');
    expect(fixture.nativeElement.textContent).toContain('haftalık özet');
  });
});

describe('ParentNotificationSettingsComponent', () => {
  function setup(service: object) {
    TestBed.configureTestingModule({ imports: [ParentNotificationSettingsComponent], providers: [provideRouter([]),
      { provide: CoachingParentService, useValue: service }, { provide: ToasterService, useValue: toaster }] });
    const fixture = TestBed.createComponent(ParentNotificationSettingsComponent); fixture.detectChanges(); return fixture;
  }

  it('turns the weekly summary e-mail off and keeps the other channels', () => {
    const service = { getWeeklyEmail: vi.fn(() => of({ inApp: true, email: true, sms: false, whatsApp: true })),
      setWeeklyEmail: vi.fn(() => of({ inApp: true, email: false, sms: false, whatsApp: true })) };
    const fixture = setup(service);
    const toggle = fixture.nativeElement.querySelector('input[role=switch]') as HTMLInputElement;
    expect(toggle.checked).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Haftalık özet e-postası');
    toggle.click(); fixture.detectChanges();
    expect(service.setWeeklyEmail).toHaveBeenCalledWith({ inApp: true, email: false, sms: false, whatsApp: true });
    expect(fixture.nativeElement.textContent).toContain('kaydedildi');
  });

  it('shows why the choice could not be loaded', () => {
    const fixture = setup({ getWeeklyEmail: vi.fn(() => throwError(() => ({ status: 500 }))), setWeeklyEmail: vi.fn() });
    expect(fixture.nativeElement.querySelector('[role=alert]')?.textContent).toContain('yüklenemedi');
    expect(fixture.nativeElement.querySelector('input[role=switch]')).toBeNull();
  });
});

describe('ParentPersonalDataComponent', () => {
  it('lets the parent download the data they created', () => {
    TestBed.configureTestingModule({ imports: [ParentPersonalDataComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: ToasterService, useValue: toaster }] });
    const fixture = TestBed.createComponent(ParentPersonalDataComponent); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Kişisel verilerim');
    const section = fixture.nativeElement.querySelector('app-student-data-export') as HTMLElement;
    expect(section.textContent).toContain('kendi');
  });
});
