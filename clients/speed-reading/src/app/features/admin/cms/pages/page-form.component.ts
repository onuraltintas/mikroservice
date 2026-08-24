import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatExpansionModule } from '@angular/material/expansion';
import { QuillModule } from 'ngx-quill';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { finalize } from 'rxjs/operators';
import { CreatePageRequest, UpdatePageRequest } from '../../../../core/models/cms.models';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatButtonToggleModule, MatButtonToggleChange } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
    selector: 'app-page-form',
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
        MatSlideToggleModule,
        MatCardModule,
        MatIconModule,
        MatTabsModule,
        MatExpansionModule,
        QuillModule,
        MatButtonToggleModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './page-form.component.html',
    styleUrls: ['./page-form.component.scss']
})
export class PageFormComponent extends BaseComponent implements OnInit {
    private cmsService = inject(CmsService);
    private fb = inject(FormBuilder);
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private sanitizer = inject(DomSanitizer);

    form!: FormGroup;
    isEditMode = false;
    pageId: string | null = null;
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
            ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
            ['blockquote', 'code-block'],
            [{ 'header': 1 }, { 'header': 2 }],               // custom button values
            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
            [{ 'script': 'sub' }, { 'script': 'super' }],      // superscript/subscript
            [{ 'indent': '-1' }, { 'indent': '+1' }],          // outdent/indent
            [{ 'direction': 'rtl' }],                         // text direction
            [{ 'size': ['small', false, 'large', 'huge'] }],  // custom dropdown
            [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
            [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
            [{ 'font': [] }],
            [{ 'align': [] }],
            ['clean'],                                         // remove formatting button
            ['link', 'image', 'video']                         // link and image, video
        ]
    };

    ngOnInit() {
        this.initForm();
        this.checkEditMode();
    }

    initForm() {
        this.form = this.fb.group({
            title: ['', Validators.required],
            slug: ['', [Validators.pattern('^[a-z0-9-]+$')]], // Optional, backend generates if empty
            content: ['', Validators.required],
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
        this.pageId = this.route.snapshot.paramMap.get('id');
        if (this.pageId) {
            this.isEditMode = true;
            this.loadPage(this.pageId);
        }
    }

    loadPage(id: string) {
        this.loading.set(true);
        this.cmsService.getPageById(id)
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (page) => {
                    this.form.patchValue({
                        title: page.title,
                        slug: page.slug,
                        content: page.content,
                        isPublished: page.isPublished,
                        seoSettings: {
                            metaTitle: page.seoSettings?.metaTitle,
                            metaDescription: page.seoSettings?.metaDescription,
                            metaKeywords: page.seoSettings?.metaKeywords,
                            noIndex: page.seoSettings?.noIndex
                        }
                    });
                },
                error: (err) => {
                    this.handleError(err, 'Sayfa yüklenirken hata oluştu');
                    this.router.navigate(['/admin/cms/pages']);
                }
            });
    }

    onSubmit() {
        if (this.form.invalid) return;

        this.loading.set(true);
        const formValue = this.form.value;

        if (this.isEditMode && this.pageId) {
            const request: UpdatePageRequest = {
                id: this.pageId,
                ...formValue
            };

            this.cmsService.updatePage(this.pageId, request)
                .pipe(finalize(() => this.loading.set(false)))
                .subscribe({
                    next: () => {
                        this.toaster.success('Sayfa güncellendi', 3000);
                        this.router.navigate(['/admin/cms/pages']);
                    },
                    error: (err) => {
                        this.handleError(err, 'Güncelleme sırasında hata oluştu');
                    }
                });
        } else {
            const request: CreatePageRequest = {
                ...formValue
            };

            this.cmsService.createPage(request)
                .pipe(finalize(() => this.loading.set(false)))
                .subscribe({
                    next: () => {
                        this.toaster.success('Sayfa oluşturuldu', 3000);
                        this.router.navigate(['/admin/cms/pages']);
                    },
                    error: (err) => {
                        this.handleError(err, 'Oluşturma sırasında hata oluştu');
                    }
                });
        }
    }
}
