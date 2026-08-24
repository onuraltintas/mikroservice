import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

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
export class NewsletterSectionComponent implements OnInit {
    private fb = inject(FormBuilder);
    private cmsService = inject(PublicCmsService);

    title = 'Haftalık Bültenimize Abone Olun';
    description = 'Hızlı okuma teknikleri, eğitim ipuçları ve özel içerikler doğrudan e-posta kutunuza gelsin.';
    benefits: string[] = [
        'Her hafta yeni teknikler ve ipuçları',
        'Özel indirimler ve kampanyalar',
        'İlham veren başarı hikayeleri'
    ];

    newsletterForm: FormGroup;
    loading = false;
    submitted = false;
    error = '';

    constructor() {
        this.newsletterForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]]
        });
    }

    ngOnInit() {
        this.cmsService.getLandingContent().subscribe({
            next: (content) => {
                if (content.blocks['newsletter_title'])       this.title       = content.blocks['newsletter_title'];
                if (content.blocks['newsletter_description']) this.description = content.blocks['newsletter_description'];
                if (content.blocks['newsletter_benefits']) {
                    try {
                        const parsed = JSON.parse(content.blocks['newsletter_benefits']);
                        if (Array.isArray(parsed) && parsed.length > 0) this.benefits = parsed;
                    } catch { /* fallback değerleri koru */ }
                }
            }
        });
    }

    onSubmit() {
        if (this.newsletterForm.invalid) return;
        this.loading = true;
        this.error = '';
        this.cmsService.subscribeNewsletter({ email: this.newsletterForm.value.email }).subscribe({
            next: () => { this.submitted = true; this.loading = false; },
            error: () => {
                this.error = 'Bir hata oluştu. Lütfen tekrar deneyin.';
                this.loading = false;
            }
        });
    }

    resetForm() {
        this.submitted = false;
        this.newsletterForm.reset();
        this.error = '';
        this.loading = false;
    }
}
