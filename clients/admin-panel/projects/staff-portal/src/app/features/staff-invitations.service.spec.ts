import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StaffInvitationsService } from './staff-invitations.service';

describe('StaffInvitationsService', () => {
  let service: StaffInvitationsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(StaffInvitationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps Coaching invitation list and cancellation requests in Identity', () => {
    service.getPending('coaching').subscribe();
    http.expectOne('/api/invitations/sent-pending').flush([]);

    service.cancel('coaching', 'invite-1').subscribe();
    const request = http.expectOne('/api/invitations/invite-1/cancel');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('keeps Speed Reading invitation list and cancellation requests in its own API', () => {
    service.getPending('speed-reading').subscribe();
    http.expectOne('/api/speed-reading/invitations/sent-pending').flush([]);

    service.cancel('speed-reading', 'invite-2').subscribe();
    const request = http.expectOne('/api/speed-reading/invitations/invite-2/cancel');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
