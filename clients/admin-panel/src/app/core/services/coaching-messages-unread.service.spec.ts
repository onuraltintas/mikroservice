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
});
