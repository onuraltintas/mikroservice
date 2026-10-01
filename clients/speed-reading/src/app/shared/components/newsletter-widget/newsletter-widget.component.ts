import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { NewsletterCaptchaService } from '../../../core/services/newsletter-captcha.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ToasterService } from '../../../core/services/toaster.service';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';

@Component({
    selector: 'app-newsletter-widget',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
    ],
    templateUrl: './newsletter-widget.component.html',
    styleUrls: ['./newsletter-widget.component.scss']
})
export class NewsletterWidgetComponent {
    private fb = inject(FormBuilder);
    private cmsService = inject(PublicCmsService);
    private legalPages = inject(PlatformLegalPagesService);
    private newsletterCaptcha = inject(NewsletterCaptchaService);
    private toaster = inject(ToasterService);

    newsletterForm: FormGroup;
    loading = false;
    isMinimized = false;
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

    async onSubmit(): Promise<void> {
        if (this.newsletterForm.invalid || this.privacyPolicyVersion === null || this.newsletterConsentVersion === null) {
            this.newsletterForm.markAllAsTouched();
            return;
        }

        this.loading = true;
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
                next: () => {
                    this.toaster.success('İsteğiniz alındı. Aboneliği tamamlamak için e-posta adresinizi doğrulayın.');
                    this.newsletterForm.reset({ email: '', consentGiven: false, honeypot: '' });
                    this.loading = false;
                    this.isMinimized = true;
                },
                error: error => {
                    const message = error instanceof HttpErrorResponse && error.status === 409
                        ? 'Gizlilik metni güncellendi. Sayfayı yenileyip tekrar deneyin.'
                        : error instanceof HttpErrorResponse && error.status === 503
                            ? 'Gizlilik metni şu anda doğrulanamıyor. Lütfen daha sonra tekrar deneyin.'
                            : 'İstek tamamlanamadı. E-posta adresinizi ve açık onayınızı kontrol edin.';
                    this.toaster.error(message);
                    this.loading = false;
                }
            });
        } catch {
            this.toaster.error('Güvenlik doğrulaması yüklenemedi. Sayfayı yenileyip tekrar deneyin.');
            this.loading = false;
        }
    }

    toggleMinimize(): void {
        this.isMinimized = !this.isMinimized;
    }

    getErrorMessage(): string {
        const emailField = this.newsletterForm.get('email');
        if (emailField?.hasError('required')) {
            return 'E-posta adresi gereklidir';
        }
        if (emailField?.hasError('email')) {
            return 'Geçerli bir e-posta adresi giriniz';
        }
        return '';
    }
}
