import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../core/services/toaster.service';
import { CoachingFinanceService } from '../../core/services/coaching-finance.service';
import { COACHING_PORTAL_ROUTES } from './coaching-portal.routes';
import { StudentTeachersComponent } from './pages/student-teachers.component';
import { StudentDataExportComponent } from './pages/student-data-export.component';
import { StudentFeeDetailComponent } from './pages/student-fee-detail.component';

// Roadmap O12: data export, leaving a teacher, removing a student and a fee detail screen.
const confirm = vi.fn().mockResolvedValue(true);
beforeEach(() => { confirm.mockClear(); confirm.mockResolvedValue(true); TestBed.overrideProvider(ToasterService, { useValue: { confirm, success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }); });
const coach = '11111111-1111-1111-1111-111111111111';
const institutionTeacher = '22222222-2222-2222-2222-222222222222';

describe('O12 my teachers', () => {
  it('lists the student\'s teachers and lets them leave an independent teacher after confirming', async () => {
    TestBed.configureTestingModule({ imports: [StudentTeachersComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentTeachersComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.method === 'GET' && r.url.endsWith('/coaching/students/me/teachers')).flush([
      { teacherUserId: coach, teacherName: 'Can Öğretmen', institutionId: null, since: '2026-09-01T00:00:00Z', canLeave: true },
      { teacherUserId: institutionTeacher, teacherName: 'Kurum Öğretmeni', institutionId: '33333333-3333-3333-3333-333333333333', since: '2026-09-01T00:00:00Z', canLeave: false }]);
    const page = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => { fixture.detectChanges(); expect(page.textContent).toContain('Can Öğretmen'); }); expect(page.textContent).toContain('Kurum Öğretmeni');
    const leaveButtons = [...page.querySelectorAll('button[data-leave-teacher]')] as HTMLButtonElement[];
    expect(leaveButtons.map(b => b.getAttribute('data-leave-teacher'))).toEqual([coach]);
    expect(page.textContent).toContain('kurum yönetimi');
    const leaving = fixture.componentInstance.leave(fixture.componentInstance.teachers()[0]);
    await vi.waitFor(() => expect(confirm).toHaveBeenCalled());
    await vi.waitFor(() => http.expectOne(r => r.method === 'DELETE' && r.url.endsWith(`/coaching/students/me/teachers/${coach}`)).flush(null));
    await vi.waitFor(() => http.expectOne(r => r.method === 'GET' && r.url.endsWith('/coaching/students/me/teachers')).flush([]));
    await leaving;
    expect(fixture.componentInstance.message()).toContain('Can Öğretmen');
    http.verify();
  });

  it('keeps the link when the student changes their mind', async () => {
    confirm.mockResolvedValue(false);
    TestBed.configureTestingModule({ imports: [StudentTeachersComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentTeachersComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/coaching/students/me/teachers')).flush([{ teacherUserId: coach, teacherName: 'Can', institutionId: null, since: '2026-09-01T00:00:00Z', canLeave: true }]);
    await vi.waitFor(() => expect(fixture.componentInstance.teachers()).toHaveLength(1));
    await fixture.componentInstance.leave(fixture.componentInstance.teachers()[0]);
    http.expectNone(r => r.method === 'DELETE'); http.verify();
  });
});

describe('O12 personal data export', () => {
  it('downloads the student\'s data as a file', async () => {
    const create = vi.fn(() => 'blob:export'); const revoke = vi.fn();
    Object.assign(URL, { createObjectURL: create, revokeObjectURL: revoke });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    TestBed.configureTestingModule({ imports: [StudentDataExportComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentDataExportComponent); fixture.detectChanges();
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(r => r.method === 'GET' && r.url.endsWith('/data-privacy/export'));
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['{}'], { type: 'application/json' }));
    await vi.waitFor(() => expect(click).toHaveBeenCalled());
    expect(create).toHaveBeenCalled();
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toMatch(/^kocluk-verilerim-\d{4}-\d{2}-\d{2}\.json$/);
    click.mockRestore(); http.verify();
  });
});

describe('O12 fee detail', () => {
  it('has its own address and shows the fee, its instalments and payments', async () => {
    expect(COACHING_PORTAL_ROUTES.some(route => route.path === 'finance/:feeId')).toBe(true);
    const fee = { id: 'fee-1', studentId: 's', institutionId: null, coachId: coach, version: 1, title: 'Ekim koçluk ücreti', currency: 'TRY', totalAmount: 1200, paidAmount: 400,
      balance: 800, overdueAmount: 0, isCancelled: false, cancellationReason: null, cancelledAt: null,
      installments: [{ id: 'i1', sequence: 1, dueOn: '2026-10-01', amount: 400, balance: 0, isOverdue: false }, { id: 'i2', sequence: 2, dueOn: '2026-11-01', amount: 800, balance: 800, isOverdue: false }] };
    const api = { fee: vi.fn(() => of(fee)), payments: vi.fn(() => of({ items: [{ id: 'p1', agreementId: 'fee-1', studentId: 's', recordedByUserId: coach, reversalOfPaymentId: null,
      method: 'BankTransfer', amount: 400, receivedOn: '2026-10-02', reference: 'EFT-1', createdAt: '2026-10-02T10:00:00Z' }], totalCount: 1, pageNumber: 1, pageSize: 25 })) };
    TestBed.configureTestingModule({ imports: [StudentFeeDetailComponent], providers: [provideRouter([]), { provide: CoachingFinanceService, useValue: api },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ feeId: 'fee-1' }) } } }] });
    const fixture = TestBed.createComponent(StudentFeeDetailComponent); fixture.detectChanges();
    await vi.waitFor(() => { fixture.detectChanges(); expect((fixture.nativeElement as HTMLElement).textContent).toContain('EFT-1'); });
    expect(api.fee).toHaveBeenCalledWith('student', '', 'fee-1'); expect(api.payments).toHaveBeenCalledWith('student', '', 'fee-1', 1);
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Ekim koçluk ücreti'); expect(text).toContain('2. taksit'); expect(text).toContain('Havale'); expect(text).toContain('EFT-1');
  });

  it('reads one fee from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(CoachingFinanceService).fee('student', '', 'fee-1').subscribe();
    TestBed.inject(HttpTestingController).expectOne(r => r.url.endsWith('/coaching/finance/me/fees/fee-1')).flush({ success: true, data: {} });
  });
});

