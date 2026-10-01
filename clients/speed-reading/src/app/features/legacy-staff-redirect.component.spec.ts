import { TestBed } from '@angular/core/testing';
import { LegacyStaffRedirectComponent } from './legacy-staff-redirect.component';
import { routes } from '../app.routes';

describe('LegacyStaffRedirectComponent', () => {
  it('offers the unified portal as the fallback for old staff URLs', () => {
    const fixture = TestBed.createComponent(LegacyStaffRedirectComponent);

    expect(fixture.componentInstance.destination).toBe('https://onuraltintas.net/staff/?product=speed-reading');
  });

  it('routes every previous teacher and institution path through the portal handoff', () => {
    const legacyRoute = routes.find(route => route.path === 'teacher');

    expect(legacyRoute?.children?.map(route => route.path)).toEqual(['', '**']);
    expect(legacyRoute?.loadChildren).toBeUndefined();
  });
});
