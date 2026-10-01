import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';
import { PublicCmsService } from './public-cms.service';

describe('PublicCmsService newsletter', () => {
  let service: PublicCmsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PublicCmsService,
        { provide: LanguageService, useValue: { currentLanguage: () => 'tr' } },
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(PublicCmsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends explicit consent, the shared privacy version and bot signals', () => {
    const payload = {
      email: 'reader@example.com',
      consentGiven: true,
      privacyPolicyVersion: 4,
      newsletterConsentVersion: 2,
      honeypot: '',
      recaptchaToken: 'captcha-token'
    };

    service.subscribeNewsletter(payload).subscribe(message => expect(message).toBe('Confirmation sent'));

    const request = http.expectOne(request => request.url.endsWith('/cms/newsletter/subscribe'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    request.flush({ message: 'Confirmation sent' });
  });

  it('confirms an email address only through the explicit confirmation endpoint', () => {
    service.confirmNewsletterSubscription('opaque-token').subscribe(message => expect(message).toBe('Confirmed'));

    const request = http.expectOne(request => request.url.endsWith('/cms/newsletter/confirm'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'opaque-token' });
    request.flush({ message: 'Confirmed' });
  });

  it('unsubscribes only through the explicit unsubscribe endpoint', () => {
    service.unsubscribeNewsletter('opaque-token').subscribe(message => expect(message).toBe('Unsubscribed'));

    const request = http.expectOne(request => request.url.endsWith('/cms/newsletter/unsubscribe'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'opaque-token' });
    request.flush({ message: 'Unsubscribed' });
  });
});
