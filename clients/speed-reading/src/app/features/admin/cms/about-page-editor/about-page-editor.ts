import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators, FormsModule } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { ContentBlock } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';
import { QuillModule } from 'ngx-quill';

@Component({
    selector: 'app-about-page-editor',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        MatTabsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        QuillModule
    ],
    templateUrl: './about-page-editor.html',
    styleUrl: './about-page-editor.scss'
})
export class AboutPageEditorComponent extends BaseComponent implements OnInit {
    private fb = inject(FormBuilder);
    private cmsService = inject(CmsService);

    aboutForm!: FormGroup;

    quillConfig = {
        toolbar: [
            ['bold', 'italic', 'underline', 'strike'],
            ['blockquote', 'code-block'],
            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
            [{ 'header': [1, 2, 3, false] }],
            ['clean'],
            ['link']
        ]
    };

    constructor() {
        super();
        this.initForm();
    }

    ngOnInit() {
        this.loadContent();
    }

    get testimonials() {
        return this.aboutForm.get('testimonials') as FormArray;
    }

    addTestimonial() {
        const testimonialGroup = this.fb.group({
            name: ['', Validators.required],
            role: ['', Validators.required],
            text: ['', Validators.required],
            rating: [5, [Validators.required, Validators.min(1), Validators.max(5)]]
        });
        this.testimonials.push(testimonialGroup);
    }

    removeTestimonial(index: number) {
        this.testimonials.removeAt(index);
    }

    private initForm() {
        this.aboutForm = this.fb.group({
            hero: this.fb.group({
                title: ['', Validators.required],
                subtitle: ['', Validators.required]
            }),
            story: this.fb.group({
                title: ['', Validators.required],
                content: ['', Validators.required]
            }),
            mission: this.fb.group({
                title: ['', Validators.required],
                content: ['', Validators.required]
            }),
            testimonials: this.fb.array([])
        });
    }

    private loadContent() {
        this.loading.set(true);
        // Request content specifically for 'AboutPage' group
        this.cmsService.getLandingContentForAdmin('AboutPage').subscribe({
            next: (blocks: ContentBlock[]) => {
                const blockMap: any = {};
                blocks.forEach(block => {
                    blockMap[block.key] = block.value;
                });

                // Hero
                if (blockMap['about_hero_title']) {
                    this.aboutForm.get('hero')?.patchValue({
                        title: blockMap['about_hero_title'],
                        subtitle: blockMap['about_hero_subtitle'] || ''
                    });
                }

                // Story
                if (blockMap['about_story_title']) {
                    this.aboutForm.get('story')?.patchValue({
                        title: blockMap['about_story_title'],
                        content: blockMap['about_story_content'] || ''
                    });
                }

                // Mission
                if (blockMap['about_mission_title']) {
                    this.aboutForm.get('mission')?.patchValue({
                        title: blockMap['about_mission_title'],
                        content: blockMap['about_mission_content'] || ''
                    });
                }

                // Testimonials
                if (blockMap['about_testimonials']) {
                    try {
                        const testimonialData = JSON.parse(blockMap['about_testimonials']);
                        this.testimonials.clear();
                        if (Array.isArray(testimonialData)) {
                            testimonialData.forEach((t: any) => {
                                this.testimonials.push(this.fb.group({
                                    name: [t.name || '', Validators.required],
                                    role: [t.role || '', Validators.required],
                                    text: [t.text || '', Validators.required],
                                    rating: [t.rating || 5, [Validators.required, Validators.min(1), Validators.max(5)]]
                                }));
                            });
                        }
                    } catch (e) {
                        console.error('Failed to parse testimonials JSON', e);
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
        if (this.aboutForm.invalid) {
            this.toaster.warning('Lütfen tüm zorunlu alanları doldurun', 3000);
            this.aboutForm.markAllAsTouched();
            return;
        }

        this.loading.set(true);
        const formData = this.aboutForm.value;

        const blocks: any = {
            about_hero_title: formData.hero.title,
            about_hero_subtitle: formData.hero.subtitle,
            about_story_title: formData.story.title,
            about_story_content: formData.story.content,
            about_mission_title: formData.mission.title,
            about_mission_content: formData.mission.content,
            about_testimonials: JSON.stringify(formData.testimonials)
        };

        // Send update request specifically for 'AboutPage' group
        this.cmsService.updateLandingContent(blocks, 'AboutPage')
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('Hakkımızda sayfası güncellendi!', 3000);
                },
                error: (err: any) => {
                    this.handleError(err, 'Kaydetme hatası');
                }
            });
    }

    onCancel() {
        this.aboutForm.reset();
        this.loadContent();
    }
}
