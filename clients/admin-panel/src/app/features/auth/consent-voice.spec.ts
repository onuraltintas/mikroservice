import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { IdentityService } from '../../core/services/identity.service';
import { AUTH_VOICE } from './auth-voice';
import { GoogleRegistrationConsentComponent } from './google-registration-consent.component';
import { RegistrationLegalConsentComponent } from './registration-legal-consent.component';

// Review L7: both consent parts are rendered on eduivme.com too (login page), so they follow the host's voice.
function render<T>(component: Type<T>, voice: 'sen' | 'siz') {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [provideRouter([]), { provide: AUTH_VOICE, useValue: voice }, { provide: IdentityService, useValue: {
      getPublicLegalPage: (slug: string) => of({ slug, title: slug.toUpperCase(), version: 3 }) } }],
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('consent copy voice', () => {
  it('Google registration consent says sen on Coaching and siz on the management panel', () => {
    expect(render(GoogleRegistrationConsentComponent, 'sen').querySelector('h2')?.textContent).toBe('Kaydını tamamla');
    TestBed.resetTestingModule();
    const siz = render(GoogleRegistrationConsentComponent, 'siz');
    expect(siz.querySelector('h2')?.textContent).toBe('Kaydınızı tamamlayın');
    expect(siz.textContent).toContain('Vazgeçerseniz');
  });

  it('the legal consent note follows the host voice', () => {
    expect(render(RegistrationLegalConsentComponent, 'sen').querySelector('[data-testid="legal-consent-note"]')?.textContent)
      .toContain('Onayladığın metinlerin');
    TestBed.resetTestingModule();
    expect(render(RegistrationLegalConsentComponent, 'siz').querySelector('[data-testid="legal-consent-note"]')?.textContent)
      .toContain('Onayladığınız metinlerin');
  });
});
