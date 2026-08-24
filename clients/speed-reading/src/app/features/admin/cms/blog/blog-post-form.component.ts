import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { QuillModule } from 'ngx-quill';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { finalize } from 'rxjs/operators';
import { CreateBlogPostRequest } from '../../../../core/models/cms.models';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatButtonToggleModule, MatButtonToggleChange } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
    selector: 'app-blog-post-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        RouterModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatCheckboxModule,
        MatCardModule,
        MatIconModule,
        MatTabsModule,
        MatExpansionModule,
        QuillModule,
        MatSlideToggleModule,
        MatChipsModule,
        MatButtonToggleModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './blog-post-form.component.html',
    styleUrls: ['./blog-post-form.component.scss']
})
export class BlogPostFormComponent extends BaseComponent implements OnInit {
    protected readonly window = window;
    private cmsService = inject(CmsService);
    private fb = inject(FormBuilder);
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private sanitizer = inject(DomSanitizer);

    form!: FormGroup;
    isEditMode = false;
    postId: string | null = null;
    seoExpanded = false;
    editorMode: 'edit' | 'preview' = 'edit';

    get previewContent(): SafeHtml {
        return this.sanitizer.bypassSecurityTrustHtml(this.form.get('content')?.value || '');
    }

    onEditorModeChange(event: MatButtonToggleChange) {
        this.editorMode = event.value as 'edit' | 'preview';
    }



    quillConfig = {
        toolbar: [
            ['bold', 'italic', 'underline', 'strike'],
            ['blockquote', 'code-block'],
            [{ 'header': 1 }, { 'header': 2 }],
            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
            [{ 'script': 'sub' }, { 'script': 'super' }],
            [{ 'indent': '-1' }, { 'indent': '+1' }],
            [{ 'direction': 'rtl' }],
            [{ 'size': ['small', false, 'large', 'huge'] }],
            [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
            [{ 'color': [] }, { 'background': [] }],
            [{ 'font': [] }],
            [{ 'align': [] }],
            ['clean'],
            ['link', 'image', 'video']
        ]
    };

    ngOnInit() {
        this.initForm();
        this.checkEditMode();
    }

    initForm() {
        this.form = this.fb.group({
            title: ['', Validators.required],
            slug: ['', [Validators.pattern('^[a-z0-9-]+$')]],
            content: ['', Validators.required],
            summary: [''],
            coverImageUrl: [''],
            author: ['', Validators.required],
            tags: [''], // We'll manage as comma-separated string in UI
            isPublished: [false],
            seoSettings: this.fb.group({
                metaTitle: [''],
                metaDescription: [''],
                metaKeywords: [''],
                noIndex: [false]
            })
        });
    }

    checkEditMode() {
        this.postId = this.route.snapshot.paramMap.get('id');
        if (this.postId) {
            this.isEditMode = true;
            this.loadPost(this.postId);
        }
    }

    loadPost(id: string) {
        this.loading.set(true);
        this.cmsService.getBlogPostById(id)
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (post) => {
                    this.form.patchValue({
                        title: post.title,
                        slug: post.slug,
                        content: post.content,
                        summary: post.summary || '', // summary from API
                        coverImageUrl: post.coverImageUrl,
                        author: post.author,
                        tags: post.tags ? post.tags.join(', ') : '',
                        isPublished: post.isPublished,
                        seoSettings: {
                            metaTitle: post.seoSettings?.metaTitle,
                            metaDescription: post.seoSettings?.metaDescription,
                            metaKeywords: post.seoSettings?.metaKeywords,
                            noIndex: post.seoSettings?.noIndex
                        }
                    });
                },
                error: (err) => {
                    this.handleError(err, 'Yazı yüklenirken hata oluştu');
                    this.router.navigate(['/admin/cms/blog']);
                }
            });
    }

    onSubmit() {
        if (this.form.invalid) return;

        this.loading.set(true);
        const formValue = this.form.value;

        // Convert keys -> array
        const tagsArray = formValue.tags
            ? formValue.tags.split(',').map((t: string) => t.trim()).filter((t: string) => t.length > 0)
            : [];

        const request: CreateBlogPostRequest = {
            title: formValue.title,
            slug: formValue.slug,
            content: formValue.content,
            summary: formValue.summary,
            coverImageUrl: formValue.coverImageUrl,
            author: formValue.author,
            tags: tagsArray,
            isPublished: formValue.isPublished,
            seoSettings: formValue.seoSettings
        };

        if (this.isEditMode && this.postId) {
            this.cmsService.updateBlogPost(this.postId, request)
                .pipe(finalize(() => this.loading.set(false)))
                .subscribe({
                    next: () => {
                        this.toaster.success('Blog yazısı güncellendi', 3000);
                        this.router.navigate(['/admin/cms/blog']);
                    },
                    error: (err) => {
                        this.handleError(err, 'Güncelleme sırasında hata oluştu');
                    }
                });
        } else {
            this.cmsService.createBlogPost(request)
                .pipe(finalize(() => this.loading.set(false)))
                .subscribe({
                    next: () => {
                        this.toaster.success('Blog yazısı oluşturuldu', 3000);
                        this.router.navigate(['/admin/cms/blog']);
                    },
                    error: (err) => {
                        this.handleError(err, 'Oluşturma sırasında hata oluştu');
                    }
                });
        }
    }
}
