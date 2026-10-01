import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService } from './coaching-management.service';
import { CoachingNewsletterCaptchaService } from './coaching-newsletter-captcha.service';

describe('CoachingNewsletterCaptchaService', () => {
  it('does not load or execute reCAPTCHA when the server has it disabled', async () => {
    const coaching = { getCoachingNewsletterRecaptchaConfiguration: vi.fn(() => of({ enabled: false, siteKey: null })) };
    TestBed.configureTestingModule({
      providers: [
        CoachingNewsletterCaptchaService,
        { provide: CoachingManagementService, useValue: coaching }
      ]
    });

    const token = await TestBed.inject(CoachingNewsletterCaptchaService).createToken();

    expect(token).toBeUndefined();
  });

  it('executes a token with the newsletter action when reCAPTCHA is enabled', async () => {
    const execute = vi.fn(() => Promise.resolve('captcha-token'));
    const previous = (window as any).grecaptcha;
    (window as any).grecaptcha = { ready: (callback: () => void) => callback(), execute };
    const coaching = { getCoachingNewsletterRecaptchaConfiguration: vi.fn(() => of({ enabled: true, siteKey: 'public-site-key' })) };
    TestBed.configureTestingModule({
      providers: [
        CoachingNewsletterCaptchaService,
        { provide: CoachingManagementService, useValue: coaching }
      ]
    });

    try {
      const token = await TestBed.inject(CoachingNewsletterCaptchaService).createToken();

      expect(token).toBe('captcha-token');
      expect(execute).toHaveBeenCalledWith('public-site-key', { action: 'newsletter_signup' });
    } finally {
      (window as any).grecaptcha = previous;
    }
  });
});
