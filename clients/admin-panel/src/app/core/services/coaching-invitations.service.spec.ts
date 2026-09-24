import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingInvitationsService } from './coaching-invitations.service';

describe('CoachingInvitationsService', () => {
  it('sends a scoped institution student invitation with an optional teacher', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingInvitationsService);
    service.inviteStudent('student@example.com', 'teacher-id').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/institution/invite-student'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ studentEmail: 'student@example.com', teacherUserId: 'teacher-id' });
    request.flush({ invitationId: 'invitation-id' });
    TestBed.inject(HttpTestingController).verify();
  });

  it('loads and accepts invitations for the authenticated recipient', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingInvitationsService);
    service.getMyInvitations().subscribe();
    const list = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/invitations/my-invitations'));
    expect(list.request.method).toBe('GET');
    list.flush([]);
    service.accept('invitation-id').subscribe();
    const accept = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/invitations/invitation-id/accept'));
    expect(accept.request.method).toBe('POST');
    accept.flush(null);
    TestBed.inject(HttpTestingController).verify();
  });

  it('loads only the authenticated sender invitation statuses', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingInvitationsService);
    service.getSentInvitations().subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/invitations/sent'));
    expect(request.request.method).toBe('GET');
    request.flush([]);
    TestBed.inject(HttpTestingController).verify();
  });

  it('lets a teacher invite an independent student by email', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingInvitationsService);
    service.inviteTeacherStudent('student@example.com').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/teachers/invite-student'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ studentEmail: 'student@example.com' });
    request.flush({ invitationId: 'invite-1' });
    TestBed.inject(HttpTestingController).verify();
  });
});
