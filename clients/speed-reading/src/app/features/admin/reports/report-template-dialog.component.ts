import { Component, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ReportTemplatesService } from '../../../core/services/report-templates.service';
import { ReportTemplate } from '../../../core/models/report.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-report-template-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    ReactiveFormsModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Düzenle' : 'Oluştur' }} - Rapor Şablonu</h2>
    <mat-dialog-content>
        <form [formGroup]="form">
            <mat-form-field appearance="outline" class="full-width">
                <mat-label>İsim</mat-label>
                <input matInput formControlName="name" required placeholder="Örn: Aylık Öğrenci Raporu">
            </mat-form-field>
            
            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Açıklama</mat-label>
                <textarea matInput formControlName="description" rows="3" placeholder="Raporun içeriği hakkında kısa bilgi..."></textarea>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Erişim Seviyesi (Rol)</mat-label>
                <mat-select formControlName="reportType" required>
                    <mat-option [value]="0">Öğrenci</mat-option>
                    <mat-option [value]="1">Öğretmen</mat-option>
                    <mat-option [value]="2">Yönetici</mat-option>
                </mat-select>
                <mat-hint>Bu raporu kimler görebilecek?</mat-hint>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Rapor Kategorisi</mat-label>
                <mat-select formControlName="category" required>
                    <mat-optgroup label="Genel">
                        <mat-option [value]="0">Genel Pano (Dashboard)</mat-option>
                    </mat-optgroup>
                    <mat-optgroup label="Öğrenci Metrikleri">
                        <mat-option [value]="1">Okuma Hızı</mat-option>
                        <mat-option [value]="2">Anlama Oranı</mat-option>
                        <mat-option [value]="3">Seri İlerlemesi</mat-option>
                        <mat-option [value]="4">Aktivite/Katılım</mat-option>
                    </mat-optgroup>
                    <mat-optgroup label="Sınıf Yönetimi">
                        <mat-option [value]="5">Sınıf Genel Bakış</mat-option>
                        <mat-option [value]="6">Öğrenci Detayı</mat-option>
                    </mat-optgroup>
                    <mat-optgroup label="Yönetim Paneli">
                        <mat-option [value]="11">Platform Kullanımı</mat-option>
                        <mat-option [value]="12">İçerik Analizi</mat-option>
                        <mat-option [value]="13">Sistem Sağlığı</mat-option>
                        <mat-option [value]="10">Kurum Raporları</mat-option>
                    </mat-optgroup>
                </mat-select>
            </mat-form-field>

            <div class="metrics-section">
                <label class="metrics-label">Rapor İçeriğinde Görünecek Metrikler:</label>
                <div class="checkbox-grid">
                    @for (metric of availableMetrics; track metric) {
                        <mat-checkbox 
                            [checked]="isMetricSelected(metric)" 
                            (change)="toggleMetric(metric, $event.checked)">
                            {{ metric }}
                        </mat-checkbox>
                    }
                </div>
            </div>
        </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
        <button mat-button (click)="cancel()">İptal</button>
        <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Kaydediliyor...' : 'Kaydet' }}
        </button>
    </mat-dialog-actions>
  `,
  styleUrls: ['./report-template-dialog.component.scss']
})
export class ReportTemplateDialogComponent extends BaseComponent {
  private fb = inject(FormBuilder);
  private templatesService = inject(ReportTemplatesService);
  private dialogRef = inject(MatDialogRef<ReportTemplateDialogComponent>);

  form: FormGroup;
  availableMetrics = [
    'Performans', 'Katılım', 'İlerleme', 'Tamamlama',
    'Aktiviteler', 'Süre', 'Başarı Oranı', 'Hata Oranı',
    'WPM Grafiği', 'Anlama Grafiği', 'Isı Haritası'
  ];
  selectedMetrics: string[] = [];

  constructor(@Inject(MAT_DIALOG_DATA) public data: ReportTemplate | null) {
    super();

    const tmpl = data as any;

    let metricsFromConfig: string[] = [];
    if (tmpl?.configurationJson) {
      try {
        const config = JSON.parse(tmpl.configurationJson);
        if (config && Array.isArray(config.metrics)) {
          metricsFromConfig = config.metrics;
        }
      } catch (e) {
        console.warn('Failed to parse configurationJson:', e);
      }
    }

    // Determine safe INT values for Type and Category
    let initialType = 0; // Default Student
    if (tmpl?.type !== undefined) {
      if (typeof tmpl.type === 'number') initialType = tmpl.type;
      else if (typeof tmpl.type === 'string') initialType = this.mapTypeStringToInt(tmpl.type);
    }

    let initialCategory = 0; // Default Dashboard
    if (tmpl?.category !== undefined) {
      if (typeof tmpl.category === 'number') initialCategory = tmpl.category;
      else if (typeof tmpl.category === 'string') initialCategory = this.mapCategoryStringToInt(tmpl.category);
    }

    this.form = this.fb.group({
      name: [data?.name || '', Validators.required],
      description: [data?.description || ''],
      reportType: [initialType, Validators.required],
      category: [initialCategory, Validators.required]
    });

    this.selectedMetrics = tmpl?.metrics || metricsFromConfig || [];
  }

  isMetricSelected(metric: string): boolean {
    return this.selectedMetrics.includes(metric);
  }

  toggleMetric(metric: string, checked: boolean): void {
    if (checked) {
      if (!this.selectedMetrics.includes(metric)) {
        this.selectedMetrics.push(metric);
      }
    } else {
      this.selectedMetrics = this.selectedMetrics.filter(m => m !== metric);
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }

  save(): void {
    if (this.form.invalid) return;

    this.loading.set(true);
    const formValue = this.form.value;

    const config = {
      metrics: this.selectedMetrics,
      filters: {},
      uiOptions: {
        showCharts: true,
        showTables: true
      }
    };

    // Construct command with INT values (guaranteed by MatSelect [value]="int")
    const command = {
      name: formValue.name,
      description: formValue.description,
      type: Number(formValue.reportType),
      category: Number(formValue.category),
      configurationJson: JSON.stringify(config)
    };

    const request$ = this.data
      ? this.templatesService.updateTemplate(this.data.id, command as any)
      : this.templatesService.createTemplate(command as any);

    request$.subscribe({
      next: () => {
        this.loading.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.loading.set(false);
        this.handleError(err, 'Şablon kaydedilirken bir hata oluştu');
      }
    });
  }

  private mapTypeStringToInt(type: string): number {
    switch (type) {
      case 'Student': return 0;
      case 'Teacher': return 1;
      case 'Admin': return 2;
      default: return 0;
    }
  }

  private mapCategoryStringToInt(category: string): number {
    // Enum ReportCategory mapping
    switch (category) {
      case 'Dashboard': return 0;
      case 'ReadingSpeed': return 1;
      case 'Comprehension': return 2;
      case 'Series': return 3;
      case 'Activity': return 4;
      case 'ClassOverview': return 5;
      case 'StudentDetail': return 6;
      case 'Assignment': return 7;
      case 'CategoryAnalysis': return 8;
      case 'TimeBasedProgress': return 9;
      case 'Institution': return 10;
      case 'PlatformUsage': return 11;
      case 'ContentAnalysis': return 12;
      case 'SystemHealth': return 13;
      default: return 0;
    }
  }
}
