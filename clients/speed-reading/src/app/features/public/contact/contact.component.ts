import { Component, DOCUMENT, inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormGroupDirective } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseComponent } from '../../../core/components/base.component';
import { ContactMessageRequest, GoogleRecaptchaConfiguration, PublicCmsService } from '../../../core/services/public-cms.service';
import { SeoService } from '../../../core/services/seo.service';
import { from } from 'rxjs';
import { finalize, switchMap } from 'rxjs/operators';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { isTrustedMapEmbedUrl } from '../../../core/security/trusted-resource-url';

import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-contact',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatProgressSpinnerModule,
        NavbarComponent,
        FooterComponent
    ],
    templateUrl: './contact.component.html',
    styleUrl: './contact.component.scss'
})
export class ContactComponent extends BaseComponent {
    private cmsService = inject(PublicCmsService);
    private fb = inject(FormBuilder);
    private sanitizer = inject(DomSanitizer);
    private seoService = inject(SeoService);
    private document = inject(DOCUMENT);

    contactForm!: FormGroup;
    @ViewChild(FormGroupDirective) formDir!: FormGroupDirective;

    heroTitle = 'İletişim';
    heroSubtitle = 'Sorularınız mı var? Size yardımcı olmaktan mutluluk duyarız!';

    contactInfo = {
        email: 'destek@hizliokuma.com',
        phone: '+90 (212) 123 45 67',
        address: 'İstanbul, Türkiye',
        workingHours: 'Pazartesi - Cuma\n09:00 - 18:00'
    };

    mapUrl: SafeResourceUrl | null = null;
    recaptchaConfiguration: GoogleRecaptchaConfiguration | null = null;
    private recaptchaScript?: Promise<void>;

    constructor() {
        super();
        this.initForm();
        this.loadContent();
        this.loadRecaptchaConfiguration();
    }

    private initForm() {
        this.contactForm = this.fb.group({
            name: ['', [Validators.required, Validators.minLength(2)]],
            email: ['', [Validators.required, Validators.email]],
            subject: ['', Validators.required],
            message: ['', [Validators.required, Validators.minLength(10)]]
        });
    }

    private loadContent() {
        this.cmsService.getLandingContent('ContactPage').subscribe({
            next: (content) => {
                if (content.blocks['contact_hero_title']) this.heroTitle = content.blocks['contact_hero_title'];
                if (content.blocks['contact_hero_subtitle']) this.heroSubtitle = content.blocks['contact_hero_subtitle'];

                this.seoService.updateTags({
                    title: content.blocks['contact_seo_title'] || `${this.heroTitle} | Master Hızlı Okuma`,
                    description: content.blocks['contact_seo_description'] || this.heroSubtitle,
                    keywords: content.blocks['contact_seo_keywords'] || undefined,
                    url: window.location.href,
                    type: 'website'
                });

                if (content.blocks['contact_email']) this.contactInfo.email = content.blocks['contact_email'];
                if (content.blocks['contact_phone']) this.contactInfo.phone = content.blocks['contact_phone'];
                if (content.blocks['contact_address']) this.contactInfo.address = content.blocks['contact_address'];
                if (content.blocks['contact_working_hours']) this.contactInfo.workingHours = content.blocks['contact_working_hours'];

                const mapUrl = content.blocks['contact_map_url'];
                if (isTrustedMapEmbedUrl(mapUrl)) {
                    this.mapUrl = this.sanitizer.bypassSecurityTrustResourceUrl(mapUrl);
                }
            },
            error: (err) => console.warn('Failed to load contact content', err)
        });
    }

    private loadRecaptchaConfiguration() {
        this.cmsService.getGoogleRecaptchaConfiguration().subscribe({
            next: configuration => this.recaptchaConfiguration = configuration,
            error: () => this.recaptchaConfiguration = null
        });
    }

    onSubmit() {
        if (this.contactForm.valid) {
            this.loading.set(true);
            const contact = this.contactForm.getRawValue() as ContactMessageRequest;
            from(this.getRecaptchaToken())
                .pipe(
                    switchMap(recaptchaToken => this.cmsService.submitContact({ ...contact, recaptchaToken })),
                    finalize(() => this.loading.set(false))
                )
                .subscribe({
                    next: () => {
                        this.toaster.success('Mesajınız başarıyla gönderildi! En kısa sürede size dönüş yapacağız.', 5000);
                        this.formDir.resetForm();
                        this.contactForm.reset();
                    },
                    error: (err) => {
                        this.handleError(err, 'Mesaj gönderilirken bir hata oluştu. Lütfen tekrar deneyin.');
                    }
                });
        } else {
            this.toaster.warning('Lütfen tüm alanları doğru şekilde doldurun.', 3000);
        }
    }

    private async getRecaptchaToken(): Promise<string | undefined> {
        const configuration = this.recaptchaConfiguration;
        if (!configuration?.enabled) return undefined;
        if (!configuration.siteKey) throw new Error('reCAPTCHA site key is missing.');

        await this.loadRecaptchaScript(configuration.siteKey);
        return window.grecaptcha.execute(configuration.siteKey, { action: 'contact_submit' });
    }

    private loadRecaptchaScript(siteKey: string): Promise<void> {
        if (this.recaptchaScript) return this.recaptchaScript;

        this.recaptchaScript = new Promise((resolve, reject) => {
            const ready = () => window.grecaptcha.ready(resolve);
            if (window.grecaptcha) {
                ready();
                return;
            }

            const script = this.document.createElement('script');
            script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`;
            script.async = true;
            script.defer = true;
            script.onload = ready;
            script.onerror = () => reject(new Error('reCAPTCHA could not be loaded.'));
            this.document.head.appendChild(script);
        });

        return this.recaptchaScript;
    }
}

declare global {
    interface Window {
        grecaptcha: {
            ready(callback: () => void): void;
            execute(siteKey: string, options: { action: string }): Promise<string>;
        };
    }
}
