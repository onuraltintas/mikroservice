import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IDENTITY_ROUTES } from '../identity.routes';
import { RegistrationSourcesComponent } from './registration-sources';

describe('RegistrationSourcesComponent (O11)', () => {
  it('shows registrations by campaign next to the untagged ones and reloads for another period', async () => {
    TestBed.configureTestingModule({ imports: [RegistrationSourcesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RegistrationSourcesComponent);
    fixture.detectChanges();
    const first = http.expectOne(r => r.url === '/api/users/registration-sources');
    expect(first.request.params.get('product')).toBe('coaching'); expect(first.request.params.get('days')).toBe('30');
    first.flush({ from: '', to: '', totalRegistrations: 12, untagged: 7, rows: [{ utmSource: 'instagram', utmMedium: 'social', utmCampaign: 'lgs-ekim', count: 5 }] });
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="registration-total"]').textContent).toContain('5 kampanyalı');
    expect(fixture.nativeElement.textContent).toContain('lgs-ekim');

    fixture.componentInstance.days = 90; void fixture.componentInstance.load();
    http.expectOne(r => r.params.get('days') === '90').flush({ from: '', to: '', totalRegistrations: 0, untagged: 0, rows: [] });
    http.verify();
    expect(IDENTITY_ROUTES.some(route => route.path === 'registration-sources')).toBe(true);
  });
});
