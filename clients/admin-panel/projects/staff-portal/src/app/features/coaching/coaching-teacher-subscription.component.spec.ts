import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherSubscriptionComponent } from './coaching-teacher-subscription.component';
import { CoachingTeacherSubscriptionService } from './coaching-teacher-subscription.service';

describe('CoachingTeacherSubscriptionComponent', () => {
  it('shows only the teacher seat summary and assigns a currently linked student', async () => {
    const seats = {
      subscriptionId: 'subscription-1', planName: 'Öğretmen 10', accessUntil: '2027-01-01T00:00:00Z',
      includedStudentSeats: 10, usedStudentSeats: 0, students: []
    };
    const subscriptionService = {
      getTeacherPlans: vi.fn(() => of([{
        id: 'teacher-plan', name: 'Öğretmen 10', description: 'Bağımsız öğretmen planı', audience: 'Teacher' as const,
        price: 1000, isContactOnly: false, billingPeriod: 'Annual', durationDays: 365,
        includedStudentSeats: 10, features: [], isActive: true, isPublic: true
      }])),
      getBankTransferSettings: vi.fn(() => of({
        currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Örnek Banka', iban: 'TR330006100519786457841326',
        paymentInstructions: null, bankTransferEnabled: true, isPubliclyAvailable: true
      })),
      getMyBankTransferRequests: vi.fn(() => of([])),
      getMySeatSummary: vi.fn(() => of(seats)),
      assignStudent: vi.fn(() => of(void 0)),
      removeStudent: vi.fn(() => of(void 0)),
      createBankTransferRequest: vi.fn(() => of({ id: 'request-1' }))
    };
    const teacherStudents = {
      getMyStudents: vi.fn(() => of({
        items: [{ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz', assignmentStartDate: '2026-09-01' }],
        pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1
      }))
    };
    await TestBed.configureTestingModule({
      imports: [CoachingTeacherSubscriptionComponent],
      providers: [
        { provide: CoachingTeacherSubscriptionService, useValue: subscriptionService },
        { provide: CoachingTeacherStudentsService, useValue: teacherStudents }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoachingTeacherSubscriptionComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('0 / 10');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    (fixture.nativeElement.querySelector('[data-testid="teacher-seat-student-1"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(subscriptionService.assignStudent).toHaveBeenCalledWith('student-1');
    expect(subscriptionService.getMySeatSummary).toHaveBeenCalledTimes(2);
  });

  it('lets the teacher correct or withdraw a pending notice and only offers larger plans while one runs', async () => {
    const plan = (id: string, seats: number) => ({ id, name: `Öğretmen ${seats}`, description: '', audience: 'Teacher' as const, price: seats * 100,
      isContactOnly: false, billingPeriod: 'Annual', durationDays: 365, includedStudentSeats: seats, features: [], isActive: true, isPublic: true });
    const pending = { id: 'request-1', plan: plan('teacher-10', 10), amount: 1000, currency: 'TRY', paymentReference: 'EFT-TYPO', status: 'Pending', reviewNote: null, createdAt: '2026-10-01T00:00:00Z' };
    const subscriptionService = {
      getTeacherPlans: vi.fn(() => of([plan('teacher-3', 3), plan('teacher-5', 5), plan('teacher-10', 10)])),
      getBankTransferSettings: vi.fn(() => of({ currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Örnek Banka', iban: 'TR330006100519786457841326', paymentInstructions: null, bankTransferEnabled: true, isPubliclyAvailable: true })),
      getMyBankTransferRequests: vi.fn(() => of([pending])),
      getMySeatSummary: vi.fn(() => of({ subscriptionId: 'subscription-1', planName: 'Öğretmen 5', accessUntil: '2027-01-01T00:00:00Z', includedStudentSeats: 5, usedStudentSeats: 2, students: [] })),
      assignStudent: vi.fn(() => of(void 0)), removeStudent: vi.fn(() => of(void 0)), createBankTransferRequest: vi.fn(() => of({ id: 'request-2' })),
      updateBankTransferRequest: vi.fn((id: string, body: object) => of({ ...pending, ...body, id })),
      withdrawBankTransferRequest: vi.fn((id: string) => of({ ...pending, id, status: 'Withdrawn' }))
    };
    await TestBed.configureTestingModule({
      imports: [CoachingTeacherSubscriptionComponent],
      providers: [
        { provide: CoachingTeacherSubscriptionService, useValue: subscriptionService },
        { provide: CoachingTeacherStudentsService, useValue: { getMyStudents: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })) } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(CoachingTeacherSubscriptionComponent); const component = fixture.componentInstance;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();

    expect(component.isSmallerPlan(plan('teacher-3', 3))).toBe(true);
    expect(component.isSmallerPlan(plan('teacher-10', 10))).toBe(false);
    expect((fixture.nativeElement.querySelector('input[value="teacher-3"]') as HTMLInputElement).disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Yükseltme');

    component.startEdit(component.requests()[0]); component.editReference = ' EFT-FIXED '; component.editPayerName = ''; component.editNote = '';
    await component.saveEdit();
    expect(subscriptionService.updateBankTransferRequest).toHaveBeenCalledWith('request-1', { paymentReference: 'EFT-FIXED', payerName: null, note: null });
    await component.withdraw(component.requests()[0]);
    expect(subscriptionService.withdrawBankTransferRequest).toHaveBeenCalledWith('request-1');
    expect(component.statusLabel('Withdrawn')).toBe('Geri çekildi'); expect(component.statusLabel('Expired')).toBe('Süresi doldu');
  });

  it('submits the selected teacher plan and payment reference through the teacher endpoint', async () => {
    const subscriptionService = {
      getTeacherPlans: vi.fn(() => of([{
        id: 'teacher-plan', name: 'Öğretmen planı', description: '', audience: 'Teacher' as const, price: 500,
        isContactOnly: false, billingPeriod: 'Monthly', durationDays: 30, includedStudentSeats: 5,
        features: [], isActive: true, isPublic: true
      }])),
      getBankTransferSettings: vi.fn(() => of({
        currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Örnek Banka', iban: 'TR330006100519786457841326',
        paymentInstructions: null, bankTransferEnabled: true, isPubliclyAvailable: true
      })),
      getMyBankTransferRequests: vi.fn(() => of([])),
      getMySeatSummary: vi.fn(() => of(null)),
      assignStudent: vi.fn(() => of(void 0)),
      removeStudent: vi.fn(() => of(void 0)),
      createBankTransferRequest: vi.fn(() => of({ id: 'request-1' }))
    };
    await TestBed.configureTestingModule({
      imports: [CoachingTeacherSubscriptionComponent],
      providers: [
        { provide: CoachingTeacherSubscriptionService, useValue: subscriptionService },
        { provide: CoachingTeacherStudentsService, useValue: { getMyStudents: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoachingTeacherSubscriptionComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.paymentReference = ' EFT-77 ';
    fixture.componentInstance.payerName = ' Ada Teacher ';
    await fixture.componentInstance.submitRequest();
    expect(subscriptionService.createBankTransferRequest).not.toHaveBeenCalled();
    const checkbox = fixture.nativeElement.querySelector('input[name="adultPayerDeclaration"]') as HTMLInputElement;
    expect(checkbox).not.toBeNull();
    expect(checkbox.checked).toBe(false);
    checkbox.click();
    fixture.detectChanges();
    await fixture.componentInstance.submitRequest();

    expect(subscriptionService.createBankTransferRequest).toHaveBeenCalledWith({
      planId: 'teacher-plan', paymentReference: ' EFT-77 ', payerName: ' Ada Teacher ', note: '', adultPayerDeclaration: true
    });
    expect(fixture.componentInstance.success()).toContain('Ödeme bildiriminiz alındı');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
