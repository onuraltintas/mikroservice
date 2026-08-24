import { Component, inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormGroupDirective } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseComponent } from '../../../core/components/base.component';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { finalize } from 'rxjs/operators';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

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

    mapUrl: any;

    constructor() {
        super();
        this.initForm();
        this.loadContent();
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

                if (content.blocks['contact_email']) this.contactInfo.email = content.blocks['contact_email'];
                if (content.blocks['contact_phone']) this.contactInfo.phone = content.blocks['contact_phone'];
                if (content.blocks['contact_address']) this.contactInfo.address = content.blocks['contact_address'];
                if (content.blocks['contact_working_hours']) this.contactInfo.workingHours = content.blocks['contact_working_hours'];

                if (content.blocks['contact_map_url']) {
                    this.mapUrl = this.sanitizer.bypassSecurityTrustResourceUrl(content.blocks['contact_map_url']);
                }
            },
            error: (err) => console.warn('Failed to load contact content', err)
        });
    }

    onSubmit() {
        if (this.contactForm.valid) {
            this.loading.set(true);
            this.cmsService.submitContact(this.contactForm.value)
                .pipe(finalize(() => this.loading.set(false)))
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
}
