import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { NewsletterSectionComponent } from './features/public/home/sections/newsletter-section/newsletter-section';
import { NewsletterWidgetComponent } from './shared/components/newsletter-widget/newsletter-widget.component';
import { PublicCmsService } from './core/services/public-cms.service';
import { PlatformLegalPagesService } from './core/services/platform-legal-pages.service';
import { NewsletterCaptchaService } from './core/services/newsletter-captcha.service';
import { ToasterService } from './core/services/toaster.service';

for (const component of [NewsletterSectionComponent, NewsletterWidgetComponent]) {
  describe(component.name + ' consent presentation', () => {
    it('links to the full document without rendering it and requires explicit consent', async () => {
      await TestBed.configureTestingModule({
        imports: [component],
        providers: [
          { provide: PublicCmsService, useValue: {} },
          { provide: PlatformLegalPagesService, useValue: { getPage: () => of({ isPublished: true, content: 'FULL LEGAL DOCUMENT', version: 1 }) } },
          { provide: NewsletterCaptchaService, useValue: {} },
          { provide: ToasterService, useValue: {} }
        ]
      }).compileComponents();
      const fixture = TestBed.createComponent<NewsletterSectionComponent | NewsletterWidgetComponent>(component);
      fixture.detectChanges();
      const root: HTMLElement = fixture.nativeElement;
      expect(root.textContent).not.toContain('FULL LEGAL DOCUMENT');
      const link = root.querySelector<HTMLAnchorElement>('a[href="/legal/speed-reading-newsletter-consent"]')!;
      expect(link.textContent).toBe('Bülten onay metni');
      expect(link.target).toBe('_blank');
      fixture.componentInstance.newsletterForm.patchValue({ email: 'test@example.com' });
      fixture.detectChanges();
      expect(root.querySelector<HTMLInputElement>('input[type="checkbox"]')!.checked).toBeFalse();
      expect(root.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeTrue();
      fixture.componentInstance.newsletterForm.patchValue({ consentGiven: true });
      fixture.detectChanges();
      expect(root.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeFalse();
    });
  });
}
