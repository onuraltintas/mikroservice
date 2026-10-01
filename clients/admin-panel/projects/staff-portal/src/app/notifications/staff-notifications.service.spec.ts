import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { StaffNotificationsService } from './staff-notifications.service';

describe('StaffNotificationsService', () => {
  let http: HttpTestingController;
  let service: StaffNotificationsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StaffNotificationsService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(StaffNotificationsService);
  });

  afterEach(() => http.verify());

  it('uses the common notification service for Coaching and supports individual read/delete actions', async () => {
    const notifications = firstValueFrom(service.getNotifications('coaching'));
    const request = http.expectOne('/api/notifications?pageNumber=1&pageSize=100');
    request.flush([{ id: 'notice-1', title: 'Yeni seans', message: 'Yarın', isRead: false }]);
    await expect(notifications).resolves.toHaveLength(1);

    const marked = firstValueFrom(service.markAsRead('coaching', 'notice-1'));
    const markRequest = http.expectOne('/api/notifications/notice-1/mark-as-read');
    expect(markRequest.request.method).toBe('POST');
    markRequest.flush(null);
    await marked;

    const markedAll = firstValueFrom(service.markAllAsRead('coaching'));
    const markAllRequest = http.expectOne('/api/notifications/mark-all-as-read');
    expect(markAllRequest.request.method).toBe('POST');
    markAllRequest.flush(null);
    await markedAll;

    const deleted = firstValueFrom(service.deleteNotification('coaching', 'notice-1'));
    const deleteRequest = http.expectOne('/api/notifications/notice-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
    await deleted;
  });

  it('keeps Speed Reading notifications and preferences in the Speed Reading API', async () => {
    const notifications = firstValueFrom(service.getNotifications('speed-reading'));
    const request = http.expectOne('/api/speed-reading/notifications?pageNumber=1&pageSize=100');
    request.flush({ items: [{ id: 'notice-2', title: 'Yeni ödev', isRead: false }], totalCount: 1, pageNumber: 1, pageSize: 100 });
    await expect(notifications).resolves.toHaveLength(1);

    const preferences = firstValueFrom(service.getSpeedReadingPreferences());
    http.expectOne('/api/speed-reading/notifications/preferences').flush([{ id: 'pref-1', notificationType: 1, enableInApp: true, enableEmail: false, enablePush: false }]);
    await expect(preferences).resolves.toHaveLength(1);

    const saved = firstValueFrom(service.saveSpeedReadingPreferences([{ id: 'pref-1', notificationType: 1, enableInApp: false, enableEmail: true, enablePush: false }]));
    const saveRequest = http.expectOne('/api/speed-reading/notifications/preferences');
    expect(saveRequest.request.method).toBe('PUT');
    saveRequest.flush(null);
    await saved;

    const markedAll = firstValueFrom(service.markAllAsRead('speed-reading'));
    const markAllRequest = http.expectOne('/api/speed-reading/notifications/mark-all-read');
    expect(markAllRequest.request.method).toBe('PUT');
    markAllRequest.flush(null);
    await markedAll;
  });
});
