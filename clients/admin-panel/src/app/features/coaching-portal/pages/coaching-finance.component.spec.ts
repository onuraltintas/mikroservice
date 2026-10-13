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
  it('loads the authenticated learner account when opened directly as a route without inputs', () => {
    const fixture = TestBed.createComponent(CoachingFinanceComponent); fixture.detectChanges();
    expect(api.account).toHaveBeenCalledWith('student', '', 1); expect(fixture.componentInstance.account()).not.toBeNull();
  });
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
  it('cancels only an unpaid fee with an explicit reason and confirmation', () => {
    const component = setup().componentInstance; component.openCancel({ ...fee, paidAmount: 10 }); expect(component.form()).toBeNull();
    component.openCancel(fee); component.reason = 'Hatalı ücret'; component.confirmed = true; component.submit();
    expect(api.cancel).toHaveBeenCalledWith('student', 'fee', { expectedVersion: 7, reason: 'Hatalı ücret' }); expect(component.form()).toBeNull();
  });
  it('rejects invalid or future receipt dates before sending money', () => {
    const component = setup().componentInstance; component.openPayment(fee); component.amount = 10;
    for (const date of ['2026-02-31', '0000-01-01', 'not-a-date', '2999-01-01']) { component.receivedOn = date; component.submit(); }
    expect(api.pay).not.toHaveBeenCalled();
  });
  it('rejects invalid installment dates, amounts and unequal sums', () => {
    const component = setup().componentInstance; component.openCreate(); component.title = 'Paket'; component.amount = 100;
    for (const part of [{ dueOn: '2026-02-31', amount: 100 }, { dueOn: '2026-01-01', amount: -100 }, { dueOn: '2026-01-01', amount: 99 }, { dueOn: '', amount: null }]) {
      component.installments = [part]; component.submit();
    }
    expect(api.create).not.toHaveBeenCalled();
    component.installments = [{ dueOn: '2026-01-01', amount: 100 }]; component.addInstallment(); expect(component.installments).toHaveLength(2);
    component.removeInstallment(1); expect(component.installments).toHaveLength(1); component.removeInstallment(0); expect(component.installments).toHaveLength(1);
  });
  it('shows history failures and clears the account when history access is revoked', () => {
    const component = setup().componentInstance;
    api.payments.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 }))); component.loadHistory(fee);
    expect(component.historyError()).toBeTruthy(); expect(component.historyLoading()).toBe(false); expect(component.account()).not.toBeNull();
    api.payments.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 403 }))); component.loadHistory(fee);
    expect(component.account()).toBeNull(); expect(component.historyFee()).toBeNull(); expect(component.historyLoading()).toBe(false);
  });
  it('keeps a dirty form when departure is declined and warns before closing the browser', () => {
    const component = setup().componentInstance; component.openCreate();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false); component.closeForm(); expect(component.form()).toBe('create');
    const event = new Event('beforeunload', { cancelable: true }); component.beforeUnload(event as BeforeUnloadEvent); expect(event.defaultPrevented).toBe(true); confirm.mockRestore();
  });
  it('does not request cash for a learner or an invalid period', () => {
    const component = setup('student').componentInstance; component.loadCash(); expect(api.cash).not.toHaveBeenCalled();
    const staff = setup('staff').componentInstance;
    for (const dates of [['2026-10-13', '2026-10-01'], ['2024-01-01', '2026-01-01'], ['invalid', '2026-10-01']]) {
      [staff.fromDate, staff.toDate] = dates; staff.loadCash();
    }
    expect(api.cash).not.toHaveBeenCalled(); expect(staff.error()).toBeTruthy();
  });
});
