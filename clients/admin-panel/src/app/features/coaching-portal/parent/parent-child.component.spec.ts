import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingParentService } from './coaching-parent.service';
import { ParentChildComponent } from './parent-child.component';

describe('ParentChildComponent', () => {
  const link = { id: 'link-1', studentUserId: 'child-1', studentName: 'Ada Öğrenci', status: 'Verified', relationship: 'Mother' };
  function parent(overrides: Record<string, unknown> = {}) {
    return {
      getLinks: vi.fn(() => of([link])),
      getProgress: vi.fn(() => of({ totalAssignments: 4, submittedAssignments: 3, totalSessions: 2, attendedSessions: 2, upcomingSessions: 1, totalExams: 1 })),
      getExams: vi.fn(() => of({ items: [{ examId: 'e-1', examTitle: 'TYT Deneme 3', examDate: '2026-11-07T06:00:00Z', examType: 'Mock', score: 87.5, maxScore: 120 }] })),
      getAssignments: vi.fn(() => of({ items: [{ id: 'a-1', title: 'Paragraf', dueDate: '2026-11-04T15:00:00Z', status: 'Submitted', isOverdue: false }] })),
      getSessions: vi.fn(() => of({ items: [{ id: 's-1', subject: 'Haftalık görüşme', startTime: '2026-11-04T07:00:00Z', status: 'Completed', type: 'OneOnOne' }] })),
      getFees: vi.fn(() => of({ balance: 600, overdueAmount: 0, agreements: [] })),
      getAccess: vi.fn(() => of({ hasAccess: false, enforcementEnabled: true, subscriptions: [] })),
      getTransfers: vi.fn(() => of([])),
      sendTransfer: vi.fn(() => of({ id: 'transfer-1', status: 'Pending', paymentReference: 'EFT-VELI-1', plan: { name: 'Bireysel yıllık' } })),
      ...overrides
    };
  }
  function setup(service: object, id = 'child-1') {
    TestBed.configureTestingModule({ imports: [ParentChildComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ studentId: id }) } } },
      { provide: CoachingParentService, useValue: service },
      { provide: CoachingManagementService, useValue: {
        getPublicSubscriptionPlans: () => of([{ id: 'plan-1', name: 'Bireysel yıllık', audience: 'Individual', price: 1200, isContactOnly: false, isActive: true, isPublic: true, durationDays: 365, billingPeriod: 'Annual', features: [] }]),
        getPublicBankTransferSettings: () => of({ currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Banka', iban: 'TR…', paymentInstructions: null }) } },
      { provide: ToasterService, useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }] });
    const fixture = TestBed.createComponent(ParentChildComponent); fixture.detectChanges(); return fixture;
  }

  it('shows the child week read-only with Turkish labels', () => {
    const fixture = setup(parent());
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ada Öğrenci'); expect(text).toContain('TYT Deneme 3'); expect(text).toContain('Paragraf');
    expect(text).toContain('Teslim edildi'); expect(text).toContain('Tamamlandı'); expect(text).toContain('600');
    expect(fixture.nativeElement.querySelector('[data-testid="parent-subscription"]')?.textContent).toContain('etkin bir Koçluk aboneliği yok');
  });

  it('sends a transfer notice for the child only with the adult payer declaration', async () => {
    const service = parent(); const fixture = setup(service); const component = fixture.componentInstance;
    component.planId = 'plan-1'; component.paymentReference = ' EFT-VELI-1 '; component.payerName = 'Ayşe Veli';
    await component.sendTransfer();
    expect(service.sendTransfer).not.toHaveBeenCalled(); expect(component.error()).toContain('18 yaş');
    component.adultPayerDeclaration = true; await component.sendTransfer();
    expect(service.sendTransfer).not.toHaveBeenCalled(); expect(component.error()).toContain('mesafeli satış sözleşmesini');
    component.checkout = { couponCode: 'VELI-10', acceptSalesContract: true }; await component.sendTransfer();
    expect(service.sendTransfer).toHaveBeenCalledWith('child-1', { planId: 'plan-1', paymentReference: 'EFT-VELI-1', payerName: 'Ayşe Veli', note: null,
      adultPayerDeclaration: true, couponCode: 'VELI-10', acceptSalesContract: true });
    expect(component.transfers()[0].id).toBe('transfer-1');
  });

  it('shows the checkout and the parent payments (O10)', () => {
    const fixture = setup(parent());
    expect(fixture.nativeElement.querySelector('edu-coaching-checkout')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Veli olarak yaptığın ödemeler');
  });

  it('does not show a child the parent is not linked to', () => {
    const fixture = setup(parent({ getLinks: vi.fn(() => of([])) }), 'child-9');
    expect(fixture.nativeElement.textContent).toContain('Bu öğrenciyle doğrulanmış bir veli bağlantın yok');
    expect(fixture.componentInstance.loaded()).toBe(false);
  });

  it('keeps the other sections when one fails', () => {
    const fixture = setup(parent({ getFees: vi.fn(() => throwError(() => new Error('403'))) }));
    expect(fixture.nativeElement.textContent).toContain('TYT Deneme 3');
    expect(fixture.nativeElement.textContent).toContain('Ücret bilgisi yüklenemedi');
  });
});
