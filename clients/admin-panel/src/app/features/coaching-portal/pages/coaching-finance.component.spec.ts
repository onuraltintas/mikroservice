import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { CoachingFinanceComponent } from './coaching-finance.component';
import { CoachingFee, CoachingFeeAccount, CoachingFinanceService } from '../../../core/services/coaching-finance.service';

const fee: CoachingFee = { id: 'fee', studentId: 'student', institutionId: 'institution', coachId: null, version: 7, title: 'Aylık koçluk', currency: 'TRY', totalAmount: 1000, paidAmount: 0, balance: 1000, overdueAmount: 500, isCancelled: false, cancellationReason: null, cancelledAt: null, installments: [{ id: 'part', sequence: 1, dueOn: '2026-10-01', amount: 1000, balance: 1000, isOverdue: true }] };
const account = (manage = true, create = true): CoachingFeeAccount => ({ totalAmount: 1000, paidAmount: 0, balance: 1000, overdueAmount: 500, canManage: manage, canCreate: create, totalCount: 1, pageNumber: 1, pageSize: 25, agreements: [fee] });
describe('CoachingFinanceComponent', () => {
  let api: { account: ReturnType<typeof vi.fn>; payments: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>; pay: ReturnType<typeof vi.fn>; reverse: ReturnType<typeof vi.fn>; cancel: ReturnType<typeof vi.fn>; cash: ReturnType<typeof vi.fn>; receivables: ReturnType<typeof vi.fn> };
  beforeEach(() => {
    api = { account: vi.fn(() => of(account())), payments: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 })), create: vi.fn(() => of(fee)), pay: vi.fn(() => of({ ...fee, version: 8 })), reverse: vi.fn(() => of(fee)), cancel: vi.fn(() => of(fee)), cash: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, cashTotal: 0, bankTransferTotal: 0 })), receivables: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, balance: 0, overdueAmount: 0 })) };
    TestBed.configureTestingModule({ imports: [CoachingFinanceComponent], providers: [{ provide: CoachingFinanceService, useValue: api }] });
  });
  function setup(mode: 'staff' | 'student' | 'admin' = 'staff') {
    const fixture = TestBed.createComponent(CoachingFinanceComponent);
    fixture.componentRef.setInput('mode', mode); fixture.componentRef.setInput('studentId', 'student'); fixture.detectChanges();
    return fixture;
  }
  for (const mode of ['student', 'admin'] as const) it(`${mode} remains readonly even when a malformed response claims write access`, () => {
    const fixture = setup(mode), component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('[data-create-fee]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-pay-fee]')).toBeNull();
    component.openCreate(); component.openPayment(fee);
    expect(component.form()).toBeNull(); expect(api.pay).not.toHaveBeenCalled();
  });
  it('settles historical accounts without allowing new charges', () => {
    api.account.mockReturnValue(of(account(true, false)));
    const fixture = setup();
    expect(fixture.nativeElement.querySelector('[data-create-fee]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-pay-fee]')).not.toBeNull();
  });
  it('rejects overpayment and invalid cent precision before sending', () => {
    const component = setup().componentInstance; component.openPayment(fee);
    for (const amount of [1001, 1.001, -1, 0, NaN, 0.00000001, 0.01000001]) { component.amount = amount; component.submit(); }
    expect(api.pay).not.toHaveBeenCalled(); expect(component.error()).toBeTruthy();
  });
  it('checks the complete installment schedule in cents', () => {
    const component = setup().componentInstance; component.openCreate(); component.title = 'Paket'; component.amount = 0.3;
    component.installments = [{ dueOn: '2026-10-01', amount: 0.1 }, { dueOn: '2026-11-01', amount: 0.2 }];
    component.submit(); expect(api.create).toHaveBeenCalledOnce();
    const body = api.create.mock.calls[0][1]; expect(body.totalAmount).toBe(0.3); expect(body.installments).toHaveLength(2);
  });
  it('blocks departure while posting, unlocks on error and retries the exact same payload', () => {
    const post = new Subject<CoachingFee>(); api.pay.mockReturnValueOnce(post);
    const component = setup().componentInstance; component.openPayment(fee); component.amount = 250; component.submit();
    expect(component.canLeavePage()).toBe(false); expect(component.pending()).toBe(true);
    const body = api.pay.mock.calls[0][2]; post.error(new HttpErrorResponse({ status: 503 }));
    expect(component.pending()).toBe(false); expect(component.form()).toBe('payment');
    component.amount = 900; component.submit(); expect(api.pay.mock.calls[1][2]).toEqual(body);
  });
  it('preserves the form on a stale version and explicitly reloads before another attempt', () => {
    api.pay.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 409 })));
    const component = setup().componentInstance; component.openPayment(fee); component.amount = 250; component.submit();
    expect(component.conflict()).toBe(true); component.submit(); expect(api.pay).toHaveBeenCalledOnce();
    api.account.mockReturnValue(of({ ...account(), agreements: [{ ...fee, version: 9 }] }));
    component.reloadConflict(); expect(component.amount).toBe(250); component.submit();
    expect(api.pay.mock.calls[1][2].expectedVersion).toBe(9);
  });
  it('requires a reason and confirmation for reversing a receipt', () => {
    const component = setup().componentInstance;
    component.openReverse(fee, { id: 'payment', agreementId: fee.id, studentId: 'student', recordedByUserId: 'manager', reversalOfPaymentId: null, method: 'Cash', amount: 100, receivedOn: '2026-10-01', reference: null, createdAt: '2026-10-01T00:00:00Z' });
    component.reason = ''; component.confirmed = true; component.submit();
    component.reason = 'Hatalı kayıt'; component.confirmed = false; component.submit();
    expect(api.reverse).not.toHaveBeenCalled(); component.confirmed = true; component.submit(); expect(api.reverse).toHaveBeenCalledOnce();
  });
  it('clears account and permissions after access is revoked', () => {
    const component = setup().componentInstance; component.openPayment(fee); component.amount = 250;
    api.pay.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 }))); component.submit();
    expect(component.account()).toBeNull(); expect(component.form()).toBeNull(); expect(component.canManage()).toBe(false);
  });
  it('cancels old reads when switching student and ignores their delayed results', () => {
    const old = new Subject<CoachingFeeAccount>(); api.account.mockReturnValueOnce(old);
    const fixture = setup(); fixture.componentRef.setInput('studentId', 'new-student'); fixture.detectChanges();
    old.next({ ...account(), balance: 9999 }); expect(fixture.componentInstance.account()?.balance).toBe(1000);
    expect(api.account).toHaveBeenLastCalledWith('staff', 'new-student', 1);
  });
  it('shows DateOnly installment dates without a timezone conversion', () => {
    const fixture = setup(); expect(fixture.nativeElement.textContent).toContain('01.10.2026');
    expect(fixture.nativeElement.textContent).not.toContain('30.09.2026');
  });
  it('clears the previous cash account before loading another student, including failed reads', () => {
    const fixture = TestBed.createComponent(CoachingFinanceComponent); fixture.componentRef.setInput('mode', 'staff'); fixture.componentRef.setInput('cashView', true); fixture.detectChanges();
    const component = fixture.componentInstance;
    const receipt = { id: 'receipt', agreementId: fee.id, studentId: 'student', recordedByUserId: 'manager', reversalOfPaymentId: null, method: 'Cash' as const, amount: 100, receivedOn: '2026-10-01', reference: null, createdAt: '2026-10-01T00:00:00Z' };
    component.openCashAccount(receipt); expect(component.account()).not.toBeNull();
    api.account.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 })));
    component.openCashAccount({ ...receipt, studentId: 'other' }); expect(component.account()).toBeNull(); expect(component.canManage()).toBe(false);
    expect(api.account).toHaveBeenLastCalledWith('staff', 'other', 1);
  });
  it('asks once before switching from a dirty form to another cash account', () => {
    const fixture = TestBed.createComponent(CoachingFinanceComponent); fixture.componentRef.setInput('mode', 'staff'); fixture.componentRef.setInput('cashView', true); fixture.detectChanges();
    const component = fixture.componentInstance;
    const receipt = { id: 'receipt', agreementId: fee.id, studentId: 'student', recordedByUserId: 'manager', reversalOfPaymentId: null, method: 'Cash' as const, amount: 100, receivedOn: '2026-10-01', reference: null, createdAt: '2026-10-01T00:00:00Z' };
    component.openCashAccount(receipt); component.openPayment(fee);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(true).mockReturnValue(false);
    component.openCashAccount({ ...receipt, studentId: 'other' }); expect(confirm).toHaveBeenCalledOnce();
    expect(component.form()).toBeNull(); expect(api.account).toHaveBeenLastCalledWith('staff', 'other', 1); confirm.mockRestore();
  });
  it('opens an unpaid historical account from the open fee list', () => {
    api.receivables.mockReturnValue(of({ items: [fee], totalCount: 1, pageNumber: 1, pageSize: 25, balance: fee.balance, overdueAmount: fee.overdueAmount }));
    const fixture = TestBed.createComponent(CoachingFinanceComponent); fixture.componentRef.setInput('mode', 'staff'); fixture.componentRef.setInput('cashView', true); fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('[data-open-receivable="fee"]') as HTMLButtonElement;
    expect(button).not.toBeNull(); button.click(); expect(api.account).toHaveBeenLastCalledWith('staff', 'student', 1);
  });
  for (const denied of ['cash', 'receivables'] as const) it(`invalidates every parallel read after ${denied} denies access`, () => {
    const cash = new Subject<{ items: never[]; totalCount: number; pageNumber: number; pageSize: number; cashTotal: number; bankTransferTotal: number }>();
    const receivables = new Subject<{ items: CoachingFee[]; totalCount: number; pageNumber: number; pageSize: number; balance: number; overdueAmount: number }>();
    api.cash.mockReturnValue(cash); api.receivables.mockReturnValue(receivables);
    const fixture = TestBed.createComponent(CoachingFinanceComponent); fixture.componentRef.setInput('mode', 'staff'); fixture.componentRef.setInput('cashView', true); fixture.detectChanges();
    const component = fixture.componentInstance;
    if (denied === 'cash') { cash.error(new HttpErrorResponse({ status: 403 })); receivables.next({ items: [fee], totalCount: 1, pageNumber: 1, pageSize: 25, balance: 1000, overdueAmount: 400 }); }
    else { receivables.error(new HttpErrorResponse({ status: 403 })); cash.next({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, cashTotal: 500, bankTransferTotal: 0 }); }
    expect(component.cash()).toBeNull(); expect(component.receivables()).toBeNull(); expect(component.account()).toBeNull();
    expect(component.loading()).toBe(false); expect(component.receivablesLoading()).toBe(false); expect(component.pending()).toBe(false);
  });
});
