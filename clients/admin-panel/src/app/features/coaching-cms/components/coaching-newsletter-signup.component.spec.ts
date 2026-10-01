import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { IdentityService, PlatformLegalPageDto } from '../../../core/services/identity.service';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingNewsletterCaptchaService } from '../../../core/services/coaching-newsletter-captcha.service';
import { CoachingNewsletterSignupComponent } from './coaching-newsletter-signup.component';

describe('CoachingNewsletterSignupComponent', () => {
  const privacyPage: PlatformLegalPageDto = {
    slug: 'privacy', title: 'Gizlilik politikası', content: 'Onaylı ortak metin', isPublished: true,
    version: 1, createdAt: '2026-01-01T00:00:00Z'
  };

  it('does not offer newsletter signup until the common privacy page is published', () => {
    const { fixture } = setup(throwError(() => ({ status: 404 })));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('bülten kaydı kapalıdır');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('requires explicit consent and sends the address only after the user submits', async () => {
    const { fixture, coaching, captcha } = setup(of(privacyPage));
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.email.set('student@example.com');

    await component.submit();
    expect(coaching.subscribeToCoachingNewsletter).not.toHaveBeenCalled();

    component.consentGiven.set(true);
    await component.submit();
    fixture.detectChanges();
    expect(captcha.createToken).toHaveBeenCalledOnce();
    expect(coaching.subscribeToCoachingNewsletter).toHaveBeenCalledWith({
      email: 'student@example.com', consentGiven: true, privacyPolicyVersion: 1, newsletterConsentVersion: 1,
      honeypot: '', recaptchaToken: 'captcha-token'
    });
    expect(fixture.nativeElement.textContent).toContain('Adres kayıtlıysa onay bağlantısı e-posta ile gönderilecektir.');
  });

  it('silently submits the honeypot without requesting a captcha token', async () => {
    const { fixture, coaching, captcha } = setup(of(privacyPage));
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.email.set('bot@example.com');
    component.consentGiven.set(true);
    component.honeypot.set('filled-by-bot');

    await component.submit();

    expect(captcha.createToken).not.toHaveBeenCalled();
    expect(coaching.subscribeToCoachingNewsletter).toHaveBeenCalledWith({
      email: 'bot@example.com', consentGiven: true, privacyPolicyVersion: 1, newsletterConsentVersion: 1,
      honeypot: 'filled-by-bot', recaptchaToken: undefined
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Adres kayıtlıysa onay bağlantısı e-posta ile gönderilecektir.');
  });

  function setup(privacyResult: Observable<PlatformLegalPageDto>) {
    const identity = { getPublicLegalPage: vi.fn(() => privacyResult) };
    const coaching = { subscribeToCoachingNewsletter: vi.fn(() => of({ success: true, message: 'Adres kayıtlıysa onay bağlantısı e-posta ile gönderilecektir.' })) };
    const captcha = { createToken: vi.fn(() => Promise.resolve('captcha-token')) };
    TestBed.configureTestingModule({
      imports: [CoachingNewsletterSignupComponent],
      providers: [
        provideRouter([]),
        { provide: IdentityService, useValue: identity },
        { provide: CoachingManagementService, useValue: coaching },
        { provide: CoachingNewsletterCaptchaService, useValue: captcha }
      ]
    });
    return { fixture: TestBed.createComponent(CoachingNewsletterSignupComponent), coaching, captcha };
  }
});
