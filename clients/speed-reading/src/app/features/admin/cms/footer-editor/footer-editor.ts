import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { ContentBlock } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';

@Component({
    selector: 'app-footer-editor',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatTabsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './footer-editor.html',
    styleUrl: './footer-editor.scss'
})
export class FooterEditorComponent extends BaseComponent implements OnInit {
    private fb = inject(FormBuilder);
    private cmsService = inject(CmsService);

    footerForm!: FormGroup;

    socialIconOptions = [
        { value: 'facebook', label: 'Facebook' },
        { value: 'twitter', label: 'Twitter/X' },
        { value: 'instagram', label: 'Instagram' },
        { value: 'linkedin', label: 'LinkedIn' },
        { value: 'youtube', label: 'YouTube' },
        { value: 'tiktok', label: 'TikTok' },
        { value: 'whatsapp', label: 'WhatsApp' }
    ];

    constructor() {
        super();
        this.loading.set(true);
        this.initForm();
    }

    ngOnInit() {
        this.loadContent();
    }

    private initForm() {
        this.footerForm = this.fb.group({
            branding: this.fb.group({
                logoText: ['Hızlı Okuma', Validators.required],
                tagline: ['Okuma hızınızı bilimsel yöntemlerle 3 katına çıkarın', Validators.required]
            }),
            copyright: this.fb.group({
                text: ['Hızlı Okuma Platformu. Tüm hakları saklıdır.', Validators.required]
            }),
            socialLinks: this.fb.array([]),
            productLinks: this.fb.array([]),
            supportLinks: this.fb.array([]),
            legalLinks: this.fb.array([])
        });
    }

    // Social Links Helpers
    get socialLinksList(): FormArray {
        return this.footerForm.get('socialLinks') as FormArray;
    }

    createSocialLink(data: any = {}): FormGroup {
        return this.fb.group({
            icon: [data.icon || 'facebook', Validators.required],
            url: [data.url || '', Validators.required]
        });
    }

    addSocialLink() {
        this.socialLinksList.push(this.createSocialLink());
    }

    removeSocialLink(index: number) {
        this.socialLinksList.removeAt(index);
    }

    // Product Links Helpers
    get productLinksList(): FormArray {
        return this.footerForm.get('productLinks') as FormArray;
    }

    createLink(data: any = {}): FormGroup {
        return this.fb.group({
            label: [data.label || '', Validators.required],
            route: [data.route || '/', Validators.required],
            fragment: [data.fragment || '']
        });
    }

    addProductLink() {
        this.productLinksList.push(this.createLink());
    }

    removeProductLink(index: number) {
        this.productLinksList.removeAt(index);
    }

    // Support Links Helpers
    get supportLinksList(): FormArray {
        return this.footerForm.get('supportLinks') as FormArray;
    }

    addSupportLink() {
        this.supportLinksList.push(this.createLink());
    }

    removeSupportLink(index: number) {
        this.supportLinksList.removeAt(index);
    }

    // Legal Links Helpers
    get legalLinksList(): FormArray {
        return this.footerForm.get('legalLinks') as FormArray;
    }

    addLegalLink() {
        this.legalLinksList.push(this.createLink());
    }

    removeLegalLink(index: number) {
        this.legalLinksList.removeAt(index);
    }

    private loadContent() {
        this.cmsService.getLandingContentForAdmin('Footer').subscribe({
            next: (blocks: ContentBlock[]) => {
                const blockMap: any = {};
                blocks.forEach(block => {
                    blockMap[block.key] = block.value;
                });

                // Branding
                if (blockMap['footer_logo_text']) {
                    this.footerForm.get('branding')?.patchValue({
                        logoText: blockMap['footer_logo_text'],
                        tagline: blockMap['footer_tagline'] || ''
                    });
                }

                // Copyright
                if (blockMap['footer_copyright']) {
                    this.footerForm.get('copyright')?.patchValue({
                        text: blockMap['footer_copyright']
                    });
                }

                // Social Links
                if (blockMap['footer_social_links']) {
                    try {
                        const socialLinks = JSON.parse(blockMap['footer_social_links']);
                        this.socialLinksList.clear();
                        socialLinks.forEach((link: any) => {
                            this.socialLinksList.push(this.createSocialLink(link));
                        });
                    } catch (e) {
                        console.warn('Failed to parse footer_social_links');
                    }
                }

                // Product Links
                if (blockMap['footer_product_links']) {
                    try {
                        const links = JSON.parse(blockMap['footer_product_links']);
                        this.productLinksList.clear();
                        links.forEach((link: any) => {
                            this.productLinksList.push(this.createLink(link));
                        });
                    } catch (e) {
                        console.warn('Failed to parse footer_product_links');
                    }
                }

                // Support Links
                if (blockMap['footer_support_links']) {
                    try {
                        const links = JSON.parse(blockMap['footer_support_links']);
                        this.supportLinksList.clear();
                        links.forEach((link: any) => {
                            this.supportLinksList.push(this.createLink(link));
                        });
                    } catch (e) {
                        console.warn('Failed to parse footer_support_links');
                    }
                }

                // Legal Links
                if (blockMap['footer_legal_links']) {
                    try {
                        const links = JSON.parse(blockMap['footer_legal_links']);
                        this.legalLinksList.clear();
                        links.forEach((link: any) => {
                            this.legalLinksList.push(this.createLink(link));
                        });
                    } catch (e) {
                        console.warn('Failed to parse footer_legal_links');
                    }
                }

                this.loading.set(false);
            },
            error: (err: any) => {
                this.handleError(err, 'İçerik yüklenirken hata oluştu');
                this.loading.set(false);
            }
        });
    }

    onSave() {
        if (this.footerForm.invalid) {
            this.toaster.warning('Lütfen zorunlu alanları doldurun', 3000);
            return;
        }

        this.loading.set(true);
        const formData = this.footerForm.value;

        const blocks: any = {
            footer_logo_text: formData.branding.logoText,
            footer_tagline: formData.branding.tagline,
            footer_copyright: formData.copyright.text,
            footer_social_links: JSON.stringify(formData.socialLinks),
            footer_product_links: JSON.stringify(formData.productLinks),
            footer_support_links: JSON.stringify(formData.supportLinks),
            footer_legal_links: JSON.stringify(formData.legalLinks)
        };

        this.cmsService.updateLandingContent(blocks, 'Footer')
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('Footer güncellendi!', 3000);
                },
                error: (err: any) => {
                    this.handleError(err, 'Kaydetme hatası');
                }
            });
    }

    onCancel() {
        this.footerForm.reset();
        this.socialLinksList.clear();
        this.productLinksList.clear();
        this.supportLinksList.clear();
        this.legalLinksList.clear();
        this.loadContent();
    }

    trackByFn(index: number): number {
        return index;
    }
}
