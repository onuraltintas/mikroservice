import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { QuillModule } from 'ngx-quill';
import { BaseComponent } from '../../../core/components/base.component';
import { EmailTemplatesService, EmailTemplate } from '../../../core/services/email-templates.service';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-email-templates',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatChipsModule,
    MatButtonToggleModule,
    QuillModule
  ],
  templateUrl: './email-templates.component.html',
  styleUrls: ['./email-templates.component.scss']
})
export class EmailTemplatesComponent extends BaseComponent implements OnInit {
  private templatesService = inject(EmailTemplatesService);
  // toaster inherited from BaseComponent

  templates: EmailTemplate[] = [];
  displayedColumns = ['name', 'subject', 'variables', 'status', 'actions'];

  showDialog = false;
  editingTemplate: EmailTemplate | null = null;
  formData: any = {};

  showPreview = false;
  previewingTemplate: EmailTemplate | null = null;
  previewVariables: string[] = [];
  previewData: Record<string, string> = {};
  previewResult: any = null;
  previewLoading = false;

  // Editor State
  editorMode: 'editor' | 'preview' = 'editor';

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

  ngOnInit(): void {
    this.loadTemplates();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.templatesService.getTemplates()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (templates) => {
          this.templates = templates;
        },
        error: (err) => {
          this.handleError(err, 'Şablonlar yüklenirken hata oluştu');
        }
      });
  }

  getVariables(variablesString: string): string[] {
    if (!variablesString) return [];
    return variablesString.split(',').map(v => v.trim()).filter(v => v);
  }

  openCreateDialog(): void {
    this.editingTemplate = null;
    this.formData = {
      name: '',
      code: '',
      subject: '',
      body: '',
      description: '',
      availableVariables: '',
      isActive: true
    };
    this.showDialog = true;
    this.editorMode = 'editor';
  }

  openEditDialog(template: EmailTemplate): void {
    this.editingTemplate = template;
    this.formData = {
      name: template.name,
      code: template.code,
      subject: template.subject,
      body: template.body,
      description: template.description,
      availableVariables: template.availableVariables,
      isActive: template.isActive
    };
    this.showDialog = true;
    this.editorMode = 'editor';
  }

  closeDialog(): void {
    this.showDialog = false;
    this.editingTemplate = null;
    this.formData = {};
  }

  saveTemplate(): void {
    if (!this.formData.name || !this.formData.code || !this.formData.subject || !this.formData.body) {
      this.toaster.alert('Lütfen tüm zorunlu alanları doldurun');
      return;
    }

    const request = {
      name: this.formData.name,
      code: this.formData.code.toUpperCase(),
      subject: this.formData.subject,
      body: this.formData.body,
      description: this.formData.description,
      availableVariables: this.formData.availableVariables,
      isActive: this.formData.isActive
    };

    this.loading.set(true);

    if (this.editingTemplate) {
      this.templatesService.updateTemplate(this.editingTemplate.id, request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.loading.set(false))
        )
        .subscribe({
          next: () => {
            this.loadTemplates();
            this.closeDialog();
            this.handleSuccess('Şablon başarıyla güncellendi');
          },
          error: (err) => {
            this.handleError(err, 'Şablon güncellenirken hata oluştu');
          }
        });
    } else {
      this.templatesService.createTemplate(request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.loading.set(false))
        )
        .subscribe({
          next: () => {
            this.loadTemplates();
            this.closeDialog();
            this.handleSuccess('Şablon başarıyla oluşturuldu');
          },
          error: (err) => {
            this.handleError(err, 'Şablon oluşturulurken hata oluştu');
          }
        });
    }
  }

  viewTemplate(template: EmailTemplate): void {
    this.toaster.alert(`Şablon: ${template.name}\n\nKonu: ${template.subject}\n\nİçerik:\n${template.body}`);
  }

  previewTemplate(template: EmailTemplate): void {
    this.previewingTemplate = template;
    this.previewVariables = this.getVariables(template.availableVariables);
    this.previewData = {};
    this.previewVariables.forEach(v => {
      this.previewData[v] = `Örnek ${v}`;
    });
    this.showPreview = true;
    this.generatePreview();
  }

  generatePreview(): void {
    if (!this.previewingTemplate) return;

    this.previewLoading = true;
    this.templatesService.previewTemplate(this.previewingTemplate.id, this.previewData)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.previewLoading = false)
      )
      .subscribe({
        next: (result: any) => {
          this.previewResult = result;
        },
        error: (err) => {
          this.handleError(err, 'Önizleme oluşturulurken hata oluştu');
        }
      });
  }

  closePreview(): void {
    this.showPreview = false;
    this.previewingTemplate = null;
    this.previewVariables = [];
    this.previewData = {};
    this.previewResult = null;
  }

  async deleteTemplate(template: EmailTemplate): Promise<void> {
    const confirmed = await this.confirm(`"${template.name}" şablonunu silmek istediğinizden emin misiniz?`);
    if (!confirmed) {
      return;
    }

    this.loading.set(true);
    this.templatesService.deleteTemplate(template.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: () => {
          this.loadTemplates();
          this.handleSuccess('Şablon başarıyla silindi');
        },
        error: (err) => {
          this.handleError(err, 'Şablon silinirken hata oluştu');
        }
      });
  }

  placeholderExample(variable: string): string {
    return `{{${variable}}}`;
  }

  getPreviewBody(): string {
    return this.formData.body || '<p style="color: #999; text-align: center; padding: 40px;">Henüz içerik yok</p>';
  }
}
