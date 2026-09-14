import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

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

    @Input() content: NewsletterContent = DEFAULT_HOME_PAGE_CONTENT.newsletter;

    newsletterForm: FormGroup;
    loading = false;
    submitted = false;
    error = '';

    constructor() {
        this.newsletterForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]]
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
