import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingTeacherSubscriptionService } from './coaching-teacher-subscription.service';

describe('CoachingTeacherSubscriptionService', () => {
  let service: CoachingTeacherSubscriptionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CoachingTeacherSubscriptionService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CoachingTeacherSubscriptionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads only published teacher plans from the shared Coaching catalog', () => {
    let result: unknown;
    service.getTeacherPlans().subscribe(plans => result = plans);
    const request = http.expectOne('/api/coaching/subscription-plans');
    expect(request.request.method).toBe('GET');
    request.flush({ data: [
      { id: 'student-plan', audience: 'Individual', isActive: true, isPublic: true },
      { id: 'teacher-plan', audience: 'Teacher', isActive: true, isPublic: true },
      { id: 'hidden-teacher-plan', audience: 'Teacher', isActive: false, isPublic: true }
    ] });
    expect(result).toEqual([{ id: 'teacher-plan', audience: 'Teacher', isActive: true, isPublic: true }]);
  });

  it('creates a teacher-specific bank transfer request with normalized reference text', () => {
    let completed = false;
    service.createBankTransferRequest({ planId: 'teacher-plan', paymentReference: '  EFT-123  ', payerName: ' Teacher ', note: '  ' })
      .subscribe(() => completed = true);
    const request = http.expectOne('/api/coaching/subscriptions/teacher-bank-transfer-requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toMatch(/^[A-Za-z0-9._~-]{16,128}$/);
    expect(request.request.body).toEqual({ planId: 'teacher-plan', paymentReference: 'EFT-123', payerName: 'Teacher', note: null });
    request.flush({ data: { id: 'request-1' } });
    expect(completed).toBe(true);
  });

  it('uses teacher-scoped seat endpoints', () => {
    let loaded = false;
    service.getMySeatSummary().subscribe(summary => loaded = summary?.subscriptionId === 'subscription-1');
    http.expectOne('/api/coaching/subscriptions/my-teacher-subscription').flush({ data: { subscriptionId: 'subscription-1' } });
    expect(loaded).toBe(true);

    service.assignStudent('student-1').subscribe();
    const assign = http.expectOne('/api/coaching/subscriptions/my-teacher-subscription/students/student-1');
    expect(assign.request.method).toBe('PUT');
    assign.flush({ success: true });

    service.removeStudent('student-1').subscribe();
    const remove = http.expectOne('/api/coaching/subscriptions/my-teacher-subscription/students/student-1');
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ success: true });
  });
});
