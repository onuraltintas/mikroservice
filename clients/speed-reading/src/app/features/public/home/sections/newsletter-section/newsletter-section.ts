import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';
import { PlatformLegalPagesService } from '../../../../../core/services/platform-legal-pages.service';
import { NewsletterCaptchaService } from '../../../../../core/services/newsletter-captcha.service';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';

type NewsletterContent = HomeSectionHeading & { benefits: string[] };

@Component({
    selector: 'app-newsletter-section',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule
    ],
    templateUrl: './newsletter-section.html',
    styleUrl: './newsletter-section.scss'
})
export class NewsletterSectionComponent {
    private fb = inject(FormBuilder);
    private cmsService = inject(PublicCmsService);
    private legalPages = inject(PlatformLegalPagesService);
    private newsletterCaptcha = inject(NewsletterCaptchaService);

    @Input() content: NewsletterContent = DEFAULT_HOME_PAGE_CONTENT.newsletter;

    newsletterForm: FormGroup;
    loading = false;
    submitted = false;
    error = '';
    privacyPolicyVersion: number | null = null;
    newsletterConsentVersion: number | null = null;
    newsletterConsentText = '';
    privacyLoading = true;

    constructor() {
        this.newsletterForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]],
            consentGiven: [false, Validators.requiredTrue],
            honeypot: ['']
        });
        forkJoin({
            privacy: this.legalPages.getPage('privacy'),
            consent: this.legalPages.getPage('speed-reading-newsletter-consent')
        }).subscribe({
            next: ({ privacy, consent }) => {
                if (privacy.isPublished && privacy.content.trim() && privacy.version > 0) this.privacyPolicyVersion = privacy.version;
                if (consent.isPublished && consent.content.trim() && consent.version > 0) {
                    this.newsletterConsentVersion = consent.version;
                    this.newsletterConsentText = consent.content.trim();
                }
                this.privacyLoading = false;
            },
            error: () => this.privacyLoading = false
        });
    }

    async onSubmit() {
        if (this.newsletterForm.invalid || this.privacyPolicyVersion === null || this.newsletterConsentVersion === null) return;
        this.loading = true;
        this.error = '';
        try {
            const recaptchaToken = await this.newsletterCaptcha.createToken();
            this.cmsService.subscribeNewsletter({
                email: this.newsletterForm.value.email,
                consentGiven: true,
                privacyPolicyVersion: this.privacyPolicyVersion,
                newsletterConsentVersion: this.newsletterConsentVersion,
                honeypot: this.newsletterForm.value.honeypot,
                recaptchaToken
            }).subscribe({
                next: () => { this.submitted = true; this.loading = false; },
                error: error => {
                    this.error = error instanceof HttpErrorResponse && error.status === 409
                        ? 'Gizlilik metni güncellendi. Lütfen sayfayı yenileyip metni tekrar onaylayın.'
                        : error instanceof HttpErrorResponse && error.status === 503
                            ? 'Gizlilik metni şu anda doğrulanamıyor. Lütfen daha sonra tekrar deneyin.'
                            : 'İstek tamamlanamadı. Lütfen e-posta adresinizi ve onay kutusunu kontrol edip tekrar deneyin.';
                    this.loading = false;
                }
            });
        } catch {
            this.error = 'Güvenlik doğrulaması yüklenemedi. Lütfen sayfayı yenileyip tekrar deneyin.';
            this.loading = false;
        }
    }

    resetForm() {
        this.submitted = false;
        this.newsletterForm.reset({ email: '', consentGiven: false, honeypot: '' });
        this.error = '';
        this.loading = false;
    }
}
