import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingMessagesUnreadService } from './coaching-messages-unread.service';

// Paket F #36: one unread-count request per navigation; a second refresh does not abort the one in flight.
describe('CoachingMessagesUnreadService', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shares a request in flight and asks again once it has answered', () => {
    const service = TestBed.inject(CoachingMessagesUnreadService);
    const http = TestBed.inject(HttpTestingController);
    service.refresh(); service.refresh();
    const requests = http.match('/api/coaching/messages/unread-count');
    expect(requests.length).toBe(1);
    expect(requests[0].cancelled).toBe(false);
    requests[0].flush({ count: 4 });
    expect(service.count()).toBe(4);
    service.refresh();
    http.expectOne('/api/coaching/messages/unread-count').flush({ count: 0 });
    expect(service.count()).toBe(0);
  });

  // Review of PR 122: a refresh after a change (a conversation read) must not be dropped by a request in flight.
  it('a forced refresh aborts the request in flight and asks again', () => {
    const service = TestBed.inject(CoachingMessagesUnreadService);
    const http = TestBed.inject(HttpTestingController);
    service.refresh();
    const first = http.expectOne('/api/coaching/messages/unread-count');
    service.refresh({ force: true });
    expect(first.cancelled).toBe(true);
    http.expectOne('/api/coaching/messages/unread-count').flush({ count: 1 });
    expect(service.count()).toBe(1);
  });

  it('a new account starts from 0 and never gets the previous account\'s answer', () => {
    const service = TestBed.inject(CoachingMessagesUnreadService);
    const http = TestBed.inject(HttpTestingController);
    service.refresh(); http.expectOne('/api/coaching/messages/unread-count').flush({ count: 7 });
    service.refresh(); const late = http.expectOne('/api/coaching/messages/unread-count');
    service.refresh({ reset: true });
    expect(service.count()).toBe(0);
    expect(late.cancelled).toBe(true);
    http.expectOne('/api/coaching/messages/unread-count').flush({ count: 2 });
    expect(service.count()).toBe(2);
  });
});
