import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { ContentBlock } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';

@Component({
    selector: 'app-contact-page-editor',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './contact-page-editor.html',
    styleUrl: './contact-page-editor.scss'
})
export class ContactPageEditorComponent extends BaseComponent implements OnInit {
    private fb = inject(FormBuilder);
    private cmsService = inject(CmsService);

    contactForm!: FormGroup;

    constructor() {
        super();
        this.initForm();
    }

    ngOnInit() {
        this.loadContent();
    }

    private initForm() {
        this.contactForm = this.fb.group({
            heroTitle: ['', Validators.required],
            heroSubtitle: ['', Validators.required],
            email: ['', [Validators.required, Validators.email]],
            phone: ['', Validators.required],
            address: ['', Validators.required],
            workingHours: ['', Validators.required],
            mapUrl: ['']
        });
    }

    private loadContent() {
        this.loading.set(true);
        this.cmsService.getLandingContentForAdmin('ContactPage').subscribe({
            next: (blocks: ContentBlock[]) => {
                const blockMap: any = {};
                blocks.forEach(block => {
                    blockMap[block.key] = block.value;
                });

                this.contactForm.patchValue({
                    heroTitle: blockMap['contact_hero_title'] || '',
                    heroSubtitle: blockMap['contact_hero_subtitle'] || '',
                    email: blockMap['contact_email'] || '',
                    phone: blockMap['contact_phone'] || '',
                    address: blockMap['contact_address'] || '',
                    workingHours: blockMap['contact_working_hours'] || '',
                    mapUrl: blockMap['contact_map_url'] || ''
                });

                this.loading.set(false);
            },
            error: (err: any) => {
                this.handleError(err, 'İçerik yüklenirken hata oluştu');
                this.loading.set(false);
            }
        });
    }

    onSave() {
        if (this.contactForm.invalid) {
            this.toaster.warning('Lütfen tüm zorunlu alanları doldurun', 3000);
            this.contactForm.markAllAsTouched();
            return;
        }

        this.loading.set(true);
        const formData = this.contactForm.value;

        const blocks: any = {
            contact_hero_title: formData.heroTitle,
            contact_hero_subtitle: formData.heroSubtitle,
            contact_email: formData.email,
            contact_phone: formData.phone,
            contact_address: formData.address,
            contact_working_hours: formData.workingHours,
            contact_map_url: formData.mapUrl
        };

        this.cmsService.updateLandingContent(blocks, 'ContactPage')
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('İletişim sayfası güncellendi!', 3000);
                },
                error: (err: any) => {
                    this.handleError(err, 'Kaydetme hatası');
                }
            });
    }

    onCancel() {
        this.contactForm.reset();
        this.loadContent();
    }
}
