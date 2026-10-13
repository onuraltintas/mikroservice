import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingInstitutionService } from './coaching-institution.service';
import { CoachingInstitutionSubscriptionComponent } from './coaching-institution-subscription.component';

describe('CoachingInstitutionSubscriptionComponent', () => {
  const summary = { subscriptionId: 'subscription-1', planName: 'Kurum yıllık', accessUntil: '2027-01-01T00:00:00Z', includedStudentSeats: 1, usedStudentSeats: 0, students: [] };
  const student = { userId: 'student-1', firstName: 'Ada', lastName: 'Test', email: 'ada@example.test' };
  function service() { return { getSubscription: vi.fn(() => of(summary)), getStudentRoster: vi.fn(() => of({ students: [student], totalCount: 1 })), setSubscriptionSeat: vi.fn(() => of(void 0)) }; }
  async function fixture(mock = service()) {
    await TestBed.configureTestingModule({ imports: [CoachingInstitutionSubscriptionComponent], providers: [{ provide: CoachingInstitutionService, useValue: mock }] }).compileComponents();
    const fixture = TestBed.createComponent(CoachingInstitutionSubscriptionComponent); fixture.componentRef.setInput('institutionId', 'institution-1'); fixture.detectChanges();
    return fixture;
  }
  afterEach(() => { TestBed.resetTestingModule(); vi.restoreAllMocks(); });
  it('renders capacity and assigns only a roster student', async () => {
    const mock = service(); const view = await fixture(mock);
    expect(view.nativeElement.textContent).toContain('Kurum yıllık'); expect(view.nativeElement.textContent).toContain('Ada Test');
    (view.nativeElement.querySelector('[data-testid="institution-seat-student-1"]') as HTMLButtonElement).click();
    expect(mock.setSubscriptionSeat).toHaveBeenCalledWith('subscription-1', 'student-1', false);
    expect(mock.getSubscription).toHaveBeenCalledTimes(2);
  });
  it('blocks duplicate saves, refresh, paging and exit while a seat change is pending', async () => {
    const mock = service(); const pending = new Subject<undefined>(); mock.setSubscriptionSeat.mockReturnValueOnce(pending);
    const view = await fixture(mock); const component = view.componentInstance;
    component.setSeat('student-1', false); component.setSeat('student-1', false); component.load(); component.changePage(1);
    expect(mock.setSubscriptionSeat).toHaveBeenCalledTimes(1); expect(mock.getSubscription).toHaveBeenCalledTimes(1);
    expect(component.canLeavePage()).toBe(false); pending.complete(); expect(component.canLeavePage()).toBe(true);
  });
  it('rejects foreign students and full capacity without sending a request', async () => {
    const mock = service(); const view = await fixture(mock); const component = view.componentInstance;
    component.setSeat('foreign', false); expect(mock.setSubscriptionSeat).not.toHaveBeenCalled();
    component.subscription.set({ ...summary, usedStudentSeats: 1 }); component.setSeat('student-1', false);
    expect(mock.setSubscriptionSeat).not.toHaveBeenCalled();
  });
  it('clears seat and student data when scope authorization fails', async () => {
    const mock = service(); const view = await fixture(mock);
    mock.getSubscription.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 403 })));
    view.componentInstance.load(); expect(view.componentInstance.subscription()).toBeNull(); expect(view.componentInstance.students()).toEqual([]);
    expect(view.componentInstance.error()).toBeTruthy();
  });
  it('cancels a pending write and clears notices when the institution input changes', async () => {
    const mock = service(); const pending = new Subject<undefined>(); mock.setSubscriptionSeat.mockReturnValueOnce(pending);
    const view = await fixture(mock); const component = view.componentInstance;
    component.success.set('Önceki kurum'); component.setSeat('student-1', false);
    view.componentRef.setInput('institutionId', 'institution-2'); view.detectChanges();
    expect(pending.observed).toBe(false); expect(component.saving()).toBe(false); expect(component.success()).toBeNull();
    expect(mock.getSubscription).toHaveBeenCalledTimes(2);
    pending.next(undefined); expect(mock.getSubscription).toHaveBeenCalledTimes(2); expect(component.success()).toBeNull();
  });
});
