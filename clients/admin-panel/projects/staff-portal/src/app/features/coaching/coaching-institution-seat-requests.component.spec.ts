import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingInstitutionSeatRequestsComponent } from './coaching-institution-seat-requests.component';
import { CoachingInstitutionSubscriptionComponent } from './coaching-institution-subscription.component';
import { CoachingInstitutionService, CoachingSeatRequest } from './coaching-institution.service';

const flush = () => new Promise(resolve => setTimeout(resolve));
const license = { subscriptionId: 'sub-1', planName: 'Kurum yıllık', accessUntil: '2027-06-01T00:00:00Z', includedStudentSeats: 21, usedStudentSeats: 21,
  students: [], extraSeats: 1, extraSeatPrice: 120 };
const quote = { subscriptionId: 'sub-1', seats: 5, unitPrice: 120, remainingDays: 183, durationDays: 365, amount: 300.82, currency: 'TRY',
  accountHolder: 'Eduİvme', bankName: 'Banka', iban: 'TR000000000000000000000001', paymentInstructions: null };
const request = (overrides: Partial<CoachingSeatRequest> = {}): CoachingSeatRequest => ({
  id: 'r1', subscriptionId: 'sub-1', institutionId: 'i1', planName: 'Kurum yıllık', seats: 5, unitPrice: 120, remainingDays: 183, amount: 300.82,
  currency: 'TRY', paymentReference: 'EFT-K1', note: null, status: 'Pending', createdAt: '2026-11-01T09:00:00Z', reviewedAt: null, reviewNote: null,
  paymentRecordId: null, licenseEndsAt: '2027-06-01T00:00:00Z', ...overrides
});

describe('CoachingInstitutionSeatRequestsComponent (O10b)', () => {
  function service(requests: CoachingSeatRequest[] = []) {
    return {
      getSeatRequests: vi.fn(() => of(requests)), getSeatQuote: vi.fn(() => of(quote)),
      createSeatRequest: vi.fn(() => of(request())), withdrawSeatRequest: vi.fn(() => of(request({ status: 'Withdrawn' }))),
      getSubscription: vi.fn(() => of(license)), getStudentRoster: vi.fn(() => of({ students: [], totalCount: 0 })), setSubscriptionSeat: vi.fn()
    };
  }
  async function setup(mock = service()) {
    TestBed.configureTestingModule({ imports: [CoachingInstitutionSeatRequestsComponent], providers: [{ provide: CoachingInstitutionService, useValue: mock }] });
    const fixture = TestBed.createComponent(CoachingInstitutionSeatRequestsComponent);
    fixture.componentInstance.subscription = license;
    fixture.detectChanges(); await flush(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, mock };
  }
  afterEach(() => TestBed.resetTestingModule());

  it('prices extra seats for the rest of the license and sends the transfer reference', async () => {
    const { fixture, component, mock } = await setup();
    component.seats = 5;
    await component.calculate(); fixture.detectChanges();
    expect(mock.getSeatQuote).toHaveBeenCalledWith('sub-1', 5);
    const text = fixture.nativeElement.querySelector('[data-testid="seat-quote"]').textContent as string;
    expect(text).toContain('300,82 TRY'); expect(text).toContain('183 / 365 gün'); expect(text).toContain('TR000000000000000000000001');

    component.reference = ' EFT-K1 '; component.note = 'Yeni şube';
    await component.send(); fixture.detectChanges();
    expect(mock.createSeatRequest).toHaveBeenCalledWith({ subscriptionId: 'sub-1', seats: 5, paymentReference: ' EFT-K1 ', note: 'Yeni şube' });
    expect(component.success()).toContain('talebiniz alındı');
    expect(fixture.nativeElement.querySelector('[data-testid="seat-request-pending"]').textContent).toContain('inceleniyor');
  });

  it('refuses a seat count outside 1–1000 without calling the server', async () => {
    const { component, mock } = await setup();
    component.seats = 0; await component.calculate();
    expect(component.error()).toContain('1–1000'); expect(mock.getSeatQuote).not.toHaveBeenCalled();
  });

  it('shows the server reason when a request is refused', async () => {
    const mock = service();
    mock.createSeatRequest.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { message: 'Bu lisans için bekleyen bir talebiniz var.' } })) as never);
    const { component } = await setup(mock);
    await component.calculate(); component.reference = 'EFT'; await component.send();
    expect(component.error()).toBe('Bu lisans için bekleyen bir talebiniz var.');
  });

  it('lets the institution withdraw a pending request and lists past decisions', async () => {
    const { fixture, component, mock } = await setup(service([request(), request({ id: 'r0', status: 'Rejected', reviewNote: 'Havale bulunamadı' })]));
    expect(fixture.nativeElement.querySelector('[data-testid="seat-request-r0"]').textContent).toContain('Havale bulunamadı');
    await component.withdraw(component.pending()!); fixture.detectChanges();
    expect(mock.withdrawSeatRequest).toHaveBeenCalledWith('r1');
    expect(fixture.nativeElement.querySelector('[data-testid="seat-request-pending"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="seat-count"]')).toBeTruthy();
  });

  it('is offered on the license page only when the plan sells extra seats', async () => {
    const mock = service();
    TestBed.configureTestingModule({ imports: [CoachingInstitutionSubscriptionComponent], providers: [{ provide: CoachingInstitutionService, useValue: mock }] });
    const fixture = TestBed.createComponent(CoachingInstitutionSubscriptionComponent);
    fixture.componentRef.setInput('institutionId', 'i1'); fixture.detectChanges(); await flush(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('staff-coaching-institution-seat-requests')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="institution-extra-seats"]').textContent).toContain('1 ek koltuk dahil');
    TestBed.resetTestingModule();

    const without = service(); without.getSubscription.mockReturnValue(of({ ...license, extraSeats: 0, extraSeatPrice: null }) as never);
    TestBed.configureTestingModule({ imports: [CoachingInstitutionSubscriptionComponent], providers: [{ provide: CoachingInstitutionService, useValue: without }] });
    const plain = TestBed.createComponent(CoachingInstitutionSubscriptionComponent);
    plain.componentRef.setInput('institutionId', 'i1'); plain.detectChanges(); await flush(); plain.detectChanges();
    expect(plain.nativeElement.querySelector('staff-coaching-institution-seat-requests')).toBeNull();
    expect(plain.nativeElement.textContent).toContain('platform yöneticisiyle iletişime geçin');
  });
});
