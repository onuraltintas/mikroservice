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

    expect(subscriptionService.createBankTransferRequest).toHaveBeenCalledWith({
      planId: 'teacher-plan', paymentReference: ' EFT-77 ', payerName: ' Ada Teacher ', note: ''
    });
    expect(fixture.componentInstance.success()).toContain('Ödeme bildiriminiz alındı');
  });
});
