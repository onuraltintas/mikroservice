import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherSubscriptionComponent } from './coaching-teacher-subscription.component';
import { CoachingTeacherSeatSummary, CoachingTeacherSubscriptionService } from './coaching-teacher-subscription.service';

const summary = (seated: string[], capacity = 2): CoachingTeacherSeatSummary => ({
  subscriptionId: 's1', planName: 'TEST Öğretmen', accessUntil: '2026-12-15T09:00:00Z', includedStudentSeats: capacity, usedStudentSeats: seated.length,
  students: seated.map(studentId => ({ studentId, isSuspended: false, suspensionReason: null, assignedAt: '2026-11-15T09:00:00Z' }))
} as CoachingTeacherSeatSummary);
const student = (userId: string, fullName: string) => ({ userId, fullName, gradeLevel: 8 });

function setup(seats: CoachingTeacherSeatSummary | null, overrides: Record<string, unknown> = {}) {
  const subscriptions = {
    getMySeatSummary: vi.fn(() => of(seats)), assignStudent: vi.fn(() => of(undefined)), removeStudent: vi.fn(() => of(undefined)), ...overrides
  };
  const students = { getMyStudents: vi.fn(() => of({ items: [student('u1', 'Ada Öğrenci'), student('u2', 'Bora Öğrenci')], totalCount: 2, pageSize: 25 })) };
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
    { provide: CoachingTeacherSubscriptionService, useValue: subscriptions }, { provide: CoachingTeacherStudentsService, useValue: students }] });
  const fixture = TestBed.createComponent(CoachingTeacherSubscriptionComponent);
  fixture.detectChanges();
  // The shared Aboneliğim page asks the billing service on its own; these tests are about the seats.
  TestBed.inject(HttpTestingController).match(request => request.url.startsWith('/api/billing/')).forEach(request => request.flush([], { status: 503, statusText: 'x' }));
  fixture.detectChanges();
  return { fixture, subscriptions, page: fixture.componentInstance, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
}

describe('Koçluk öğretmen planı: Aboneliğim ve koltuklar', () => {
  it('shows the shared subscription page for teachers and the seats of the teacher\'s own students', () => {
    const { fixture, text } = setup(summary(['u1']));
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="teacher-billing"] staff-my-subscription')).not.toBeNull();
    expect(text()).toContain('1 / 2');
    expect(text()).toContain('Ada Öğrenci');
    // Buying, paying and the old transfer notices are no longer on this page.
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
    expect(text()).not.toContain('Ödeme referansı');
  });

  it('gives a seat while there is room and takes one back, then reads the pool again', async () => {
    const { subscriptions, page } = setup(summary(['u1']));
    await page.assignStudent(student('u2', 'Bora Öğrenci') as never);
    expect(subscriptions.assignStudent).toHaveBeenCalledWith('u2');
    expect(subscriptions.getMySeatSummary).toHaveBeenCalledTimes(2);
    await page.removeStudent(student('u1', 'Ada Öğrenci') as never);
    expect(subscriptions.removeStudent).toHaveBeenCalledWith('u1');
    expect(page.success()).toContain('kaldırıldı');
  });

  it('does not ask for a seat when the pool is full and shows the service sentence when refused', async () => {
    const full = setup(summary(['u1', 'u2']));
    await full.page.assignStudent(student('u3', 'Can Öğrenci') as never);
    expect(full.subscriptions.assignStudent).not.toHaveBeenCalled();
    TestBed.resetTestingModule();
    const refused = setup(summary(['u1']), { assignStudent: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 400, error: { message: 'Öğrenci size atanmış değil.' } }))) });
    await refused.page.assignStudent(student('u2', 'Bora Öğrenci') as never);
    expect(refused.page.error()).toBe('Öğrenci size atanmış değil.');
  });

  it('points to Aboneliğim when the teacher has no running plan', () => {
    const { text } = setup(null);
    expect(text()).toContain('Aboneliğim bölümünden bir öğretmen planı alın');
  });
});
