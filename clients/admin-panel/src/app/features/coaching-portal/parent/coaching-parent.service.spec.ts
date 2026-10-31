import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingParentService } from './coaching-parent.service';

// Roadmap Y7: the parent panel and the student's parent-link screen talk to these endpoints only.
describe('CoachingParentService', () => {
  let http: HttpTestingController; let service: CoachingParentService;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController); service = TestBed.inject(CoachingParentService);
  });
  afterEach(() => http.verify());

  it('links a parent with the child code and lets either side manage the link', () => {
    service.getLinks().subscribe(rows => expect(rows.length).toBe(1));
    http.expectOne('/api/users/me/parent-links').flush([{ id: 'link-1' }]);
    service.requestLink(' abcd-2345 ', 'Mother').subscribe();
    const request = http.expectOne('/api/users/me/parent-links');
    expect(request.request.method).toBe('POST'); expect(request.request.body).toEqual({ code: 'ABCD-2345', relationship: 'Mother' }); request.flush({ id: 'link-1' });
    service.createCode().subscribe(code => expect(code.code).toBe('ABCD-2345'));
    const code = http.expectOne('/api/users/me/parent-link-codes'); expect(code.request.method).toBe('POST'); code.flush({ code: 'ABCD-2345', expiresAt: '2026-11-09T00:00:00Z' });
    service.approveLink('link/1').subscribe();
    expect(http.expectOne('/api/users/me/parent-links/link%2F1/approve').request.method).toBe('POST');
    service.revokeLink('link/1').subscribe();
    expect(http.expectOne('/api/users/me/parent-links/link%2F1/revoke').request.method).toBe('POST');
  });

  it('reads the child data read-only and sends a transfer notice for the child', () => {
    service.getProgress('child/1').subscribe();
    http.expectOne('/api/reports/student/child%2F1/progress').flush({});
    service.getExams('child/1').subscribe();
    http.expectOne(r => r.url === '/api/exams/student/child%2F1').flush({ items: [] });
    service.getAssignments('child/1').subscribe();
    http.expectOne(r => r.url === '/api/assignments/student/child%2F1').flush({ items: [] });
    service.getSessions('child/1').subscribe();
    http.expectOne(r => r.url === '/api/sessions/student/child%2F1').flush({ items: [] });
    service.getFees('child/1').subscribe(fees => expect(fees.balance).toBe(10));
    http.expectOne('/api/coaching/finance/children/child%2F1').flush({ data: { balance: 10 } });
    service.getAccess('child/1').subscribe(access => expect(access.hasAccess).toBe(true));
    http.expectOne('/api/coaching/subscriptions/children/child%2F1/access').flush({ data: { hasAccess: true } });
    service.getTransfers('child/1').subscribe(rows => expect(rows).toEqual([]));
    http.expectOne('/api/coaching/subscriptions/children/child%2F1/bank-transfer-requests').flush({ data: [] });
    service.sendTransfer('child/1', { planId: 'plan-1', paymentReference: ' EFT-1 ', payerName: ' Ayşe ', note: ' ', adultPayerDeclaration: true }).subscribe();
    const send = http.expectOne('/api/coaching/subscriptions/children/child%2F1/bank-transfer-requests');
    expect(send.request.method).toBe('POST');
    expect(send.request.headers.get('Idempotency-Key')).toMatch(/^[A-Za-z0-9._~-]{16,128}$/);
    expect(send.request.body).toEqual({ planId: 'plan-1', paymentReference: 'EFT-1', payerName: 'Ayşe', note: null, adultPayerDeclaration: true });
    send.flush({ data: { id: 'transfer-1' } });
  });

  // V4: the parent-only reads the backend packages add (V2 speed reading, V3 coaching) and paged lists.
  it('reads the coaching week, adherence and weekly review from the parent endpoints', () => {
    service.getCoachingWeek('child/1').subscribe(week => expect(week.planned).toBe(14));
    http.expectOne('/api/coaching/parents/me/children/child%2F1/week').flush({ weekStart: '2026-10-26', planned: 14, completed: 9, overdue: [], nextSession: null, examCountdown: null, attention: [] });
    service.getCoachingWeek('child-2').subscribe(week => expect(week.completed).toBe(3));
    http.expectOne('/api/coaching/parents/me/children/child-2/week').flush({ data: { weekStart: '2026-10-26', planned: 4, completed: 3, overdue: [], nextSession: null, examCountdown: null, attention: [] } });
    service.getAdherence('child-1').subscribe(rows => expect(rows.length).toBe(1));
    const adherence = http.expectOne(r => r.url === '/api/coaching/parents/me/children/child-1/adherence');
    expect(adherence.request.params.get('weeks')).toBe('8');
    adherence.flush([{ weekStart: '2026-10-26', planned: 14, completed: 9, minutes: 300 }]);
    service.getWeeklyReview('child-1').subscribe(review => expect(review?.reviews.length).toBe(1));
    http.expectOne('/api/coaching/parents/me/children/child-1/weekly-review').flush({ weekStart: '2026-10-26', reviews: [{ note: 'İyi hafta', reviewedAt: '2026-10-31T10:00:00Z' }], decisions: [] });
  });

  it('pages assignments and sessions', () => {
    service.getAssignments('child-1', 2).subscribe();
    const assignments = http.expectOne(r => r.url === '/api/assignments/student/child-1');
    expect(assignments.request.params.get('pageNumber')).toBe('2'); expect(assignments.request.params.get('pageSize')).toBe('10');
    assignments.flush({ items: [] });
    service.getSessions('child-1', 3).subscribe();
    expect(http.expectOne(r => r.url === '/api/sessions/student/child-1').request.params.get('pageNumber')).toBe('3');
  });

  it('reads the speed-reading overview for the child', () => {
    service.getSpeedReadingOverview('child/1').subscribe(view => expect(view.program?.currentDay).toBe(12));
    http.expectOne('/api/speed-reading/parents/me/children/child%2F1/overview').flush({ studentId: 'child/1', program: { currentDay: 12, totalDays: 28 } });
  });

  it('turns the weekly summary e-mail on and off through the notification preferences', () => {
    service.getWeeklyEmail().subscribe(value => expect(value).toEqual({ inApp: true, email: true, sms: false, whatsApp: true }));
    http.expectOne('/api/notifications/preferences').flush({ data: [{ category: 'Messages', inApp: true, email: true }, { category: 'ParentSummary', inApp: true, email: true, whatsApp: true }] });
    service.setWeeklyEmail({ inApp: true, email: false, sms: false, whatsApp: true }).subscribe(value => expect(value?.email).toBe(false));
    const save = http.expectOne('/api/notifications/preferences');
    expect(save.request.method).toBe('PUT');
    // Only the weekly summary category is sent; its SMS / WhatsApp choice is kept as it was.
    expect(save.request.body).toEqual({ preferences: [{ category: 'ParentSummary', inApp: true, email: false, sms: false, whatsApp: true }] });
    save.flush({ data: [{ category: 'ParentSummary', inApp: true, email: false }] });
  });
});
