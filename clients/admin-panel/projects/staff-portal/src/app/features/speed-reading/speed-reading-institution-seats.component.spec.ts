import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { InstitutionSeatPool, SpeedReadingInstitutionSeatsComponent } from './speed-reading-institution-seats.component';
import { SpeedReadingInstitutionService } from './speed-reading-institution.service';

const institution = '22222222-2222-4222-8222-222222222222';
const url = `/api/speed-reading/institutions/${institution}/seats`;
const student = (userId: string, firstName: string) => ({ userId, firstName, lastName: 'Öğrenci', role: 'Student', isMembershipActive: true, isActive: true, createdAt: '2026-11-01T00:00:00Z' });
const pool = (seats: string[], capacity = 2): InstitutionSeatPool => ({
  subscriptionId: 's1', planName: 'TEST Kurum', endsAt: '2027-11-15T09:00:00Z', capacity, used: seats.length, extraSeats: 0, extraSeatsUntil: null, extraSeatPrice: 150,
  seats: seats.map(userId => ({ userId, isSuspended: false, assignedAt: '2026-11-15T09:00:00Z', isCovered: true }))
});

function setup(first: object) {
  const institutions = { getMembers: vi.fn(() => of({ items: [student('u1', 'Ada'), student('u2', 'Bora')], totalCount: 2, pageNumber: 1, pageSize: 100 })) };
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: SpeedReadingInstitutionService, useValue: institutions }] });
  const fixture = TestBed.createComponent(SpeedReadingInstitutionSeatsComponent);
  fixture.componentRef.setInput('institutionId', institution);
  fixture.detectChanges();
  const http = TestBed.inject(HttpTestingController);
  http.expectOne(url).flush(first);
  // The shared Aboneliğim page asks the billing service on its own; these tests are about the seats.
  const settle = () => { http.match(request => request.url.startsWith('/api/billing/')).forEach(request => request.flush([], { status: 503, statusText: 'x' })); fixture.detectChanges(); };
  settle();
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  return { fixture, http, institutions, page: fixture.componentInstance, text, settle };
}

describe('Hızlı Okuma kurum koltukları', () => {
  it('lists the active students with their seats, and whoever still sits without being a member', () => {
    const { institutions, page, text } = setup({ enabled: true, pool: pool(['u1', 'gone-user']) });
    expect(institutions.getMembers).toHaveBeenCalledWith(institution, 'Student', 1, 100, { isActive: true });
    expect(text()).toContain('2 / 2 koltuk dolu');
    expect(page.rows().map(row => [row.userId, row.isMember, !!row.seat])).toEqual([['u1', true, true], ['u2', true, false], ['gone-user', false, true]]);
    expect(text()).toContain('kurumdan ayrılmış');
  });

  it('gives and takes back a seat and shows the service sentence when the pool is full', () => {
    const { fixture, http, page, text } = setup({ enabled: true, pool: pool(['u1'], 3) });
    page.change('u2', true);
    const give = http.expectOne(`${url}/u2`);
    expect(give.request.method).toBe('PUT');
    give.flush({ enabled: true, pool: pool(['u1', 'u2'], 3) });
    fixture.detectChanges();
    expect(text()).toContain('2 / 3 koltuk dolu');

    page.change('u1', false);
    const take = http.expectOne(`${url}/u1`);
    expect(take.request.method).toBe('DELETE');
    take.flush({ code: 'Seats.NotAssigned', message: 'Bu öğrencinin koltuğu yok.' }, { status: 404, statusText: 'Not Found' });
    expect(page.error()).toBe('Bu öğrencinin koltuğu yok.');
    expect(page.busy()).toBe(false);
  });

  it('says that a plan is needed when there is none, and that the feature is off where billing does not run', () => {
    const none = setup({ enabled: true, pool: null });
    expect(none.text()).toContain('süren bir Hızlı Okuma planı yok');
    expect((none.fixture.nativeElement as HTMLElement).querySelectorAll('td button').length).toBe(0);
    TestBed.resetTestingModule();
    const off = setup({ enabled: false, pool: null });
    expect(off.text()).toContain('abonelik yönetimi açık değil');
  });
});
