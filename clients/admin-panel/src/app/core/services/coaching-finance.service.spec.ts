import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingFinanceService } from './coaching-finance.service';

describe('CoachingFinanceService', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('always uses the authenticated learner account, including multi-role users', () => {
    const service = TestBed.inject(CoachingFinanceService), http = TestBed.inject(HttpTestingController);
    service.account('student', 'other', 2).subscribe(value => expect(value.totalCount).toBe(4));
    http.expectOne('/api/coaching/finance/me?pageNumber=2&pageSize=25').flush({ success: true, data: { totalCount: 4 } });
    service.payments('student', 'other', 'fee', 3).subscribe();
    http.expectOne('/api/coaching/finance/me/fees/fee/payments?pageNumber=3&pageSize=25').flush({ success: true, data: {} });
  });
  it('uses scoped staff and readonly admin routes', () => {
    const service = TestBed.inject(CoachingFinanceService), http = TestBed.inject(HttpTestingController);
    service.account('staff', 'student').subscribe();
    http.expectOne('/api/coaching/finance/students/student?pageNumber=1&pageSize=25').flush({ success: true, data: {} });
    service.payments('admin', 'student', 'fee').subscribe();
    http.expectOne('/api/coaching-admin/finance/students/student/fees/fee/payments?pageNumber=1&pageSize=25').flush({ success: true, data: {} });
    service.cash('admin', '2026-10-01', '2026-10-13', 2).subscribe();
    http.expectOne('/api/coaching-admin/finance/cash?fromDate=2026-10-01&toDate=2026-10-13&pageNumber=2&pageSize=25').flush({ success: true, data: {} });
  });
  it('sends stable operation IDs and versions without client supplied ownership', () => {
    const service = TestBed.inject(CoachingFinanceService), http = TestBed.inject(HttpTestingController);
    const input = { requestId: 'retry-id', expectedVersion: 7, amount: 250, method: 'Cash' as const, receivedOn: '2026-10-13', reference: null };
    service.pay('student', 'fee', input).subscribe();
    const request = http.expectOne('/api/coaching/finance/students/student/fees/fee/payments');
    expect(request.request.method).toBe('POST'); expect(request.request.body).toEqual(input);
    request.flush({ success: true, data: {} });
    service.reverse('student', 'fee', 'payment', { requestId: 'reverse', expectedVersion: 8, receivedOn: '2026-10-13', reason: 'Hatalı kayıt' }).subscribe();
    http.expectOne('/api/coaching/finance/students/student/fees/fee/payments/payment/reverse').flush({ success: true, data: {} });
    service.cancel('student', 'fee', { expectedVersion: 9, reason: 'Vazgeçildi' }).subscribe();
    http.expectOne('/api/coaching/finance/students/student/fees/fee/cancel').flush({ success: true, data: {} });
  });
});
