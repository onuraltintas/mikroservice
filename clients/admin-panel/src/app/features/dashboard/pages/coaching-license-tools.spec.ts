import { vi } from 'vitest';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingLicenseEditComponent, CoachingLicenseToolsComponent } from './coaching-license-tools';

// Roadmap D6: ending / ended licenses, license edit, Excel payments export and the churn report.
describe('Coaching license tools', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const row = { id: 'l1', audience: 'Institution', planName: 'Kurum yıllık', ownerName: 'Merkez', ownerEmail: null, userId: null, institutionId: 'i1',
    status: 'Active', startDate: '2026-01-01T00:00:00Z', endDate: '2026-11-20T20:59:00Z', daysLeft: 5, seatCapacity: 10, assignedSeats: 4 };

  it('lists ending and ended licenses and shows the churn by audience', () => {
    const fixture = TestBed.createComponent(CoachingLicenseToolsComponent); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/window') && r.params.get('window') === 'ending').flush({ data: [row] });
    http.expectOne(r => r.url.endsWith('/window') && r.params.get('window') === 'ended').flush({ data: [] });
    http.expectOne(r => r.url.endsWith('/churn')).flush({ data: [
      { month: '2026-09', audience: 'Individual', ended: 2, renewed: 1, notRenewed: 1, pending: 0, churnRate: 50 },
      { month: '2026-09', audience: 'Institution', ended: 0, renewed: 0, notRenewed: 0, pending: 0, churnRate: null }] });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="licenses-ending"]')?.textContent).toContain('Merkez');
    expect(element.querySelector('[data-testid="licenses-ending"]')?.textContent).toContain('4 / 10');
    expect(element.querySelector('[data-testid="licenses-ended"]')?.textContent).toContain('Kayıt yok');
    const churn = element.querySelector('[data-testid="churn"]')!.textContent!;
    expect(churn).toContain('Bireysel'); expect(churn).toContain('%50'); expect(churn).not.toContain('Kurum');
  });

  it('sends only the changed end day (end of the Türkiye day) and seats', async () => {
    const fixture = TestBed.createComponent(CoachingLicenseEditComponent);
    fixture.componentRef.setInput('licenseId', 'l1'); fixture.componentRef.setInput('endDate', row.endDate);
    fixture.componentRef.setInput('seatCapacity', 10); fixture.componentRef.setInput('seatsEditable', true);
    fixture.detectChanges();
    const component = fixture.componentInstance; const saved: unknown[] = []; component.saved.subscribe(x => saved.push(x));
    expect(component.endDay).toBe('2026-11-20');
    component.endDay = '2027-06-30'; component.seats = 15;
    const saving = component.save();
    const request = http.expectOne(r => r.url.endsWith('/subscriptions/l1/license'));
    expect(request.request.body).toEqual({ endDate: '2027-06-30T23:59:00', seatCapacity: 15 });
    request.flush({ data: { ...row, seatCapacity: 15 } }); await saving;
    expect(saved.length).toBe(1);

    component.seats = 3; const failing = component.save();
    http.expectOne(r => r.url.endsWith('/license')).flush({ message: 'Koltuk sayısı plandaki 10 koltuktan az olamaz.' }, { status: 400, statusText: 'Bad Request' });
    await failing; expect(component.error()).toContain('plandaki 10');
  });

  it('downloads the payments as an Excel file for the chosen days', async () => {
    const fixture = TestBed.createComponent(CoachingLicenseToolsComponent); fixture.detectChanges();
    http.match(r => r.url.endsWith('/window') || r.url.endsWith('/churn')).forEach(r => r.flush({ data: [] }));
    const component = fixture.componentInstance; component.exportFrom = '2026-11-01'; component.exportTo = '2026-11-30';
    const create = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const exporting = component.exportPayments();
    const request = http.expectOne(r => r.url.endsWith('/payments/export'));
    expect([request.request.params.get('fromDate'), request.request.params.get('toDate')]).toEqual(['2026-11-01', '2026-11-30']);
    request.flush(new Blob(['x'])); await exporting;
    expect(create).toHaveBeenCalled(); expect(component.error()).toBe('');
  });
});
