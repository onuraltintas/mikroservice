import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ReportTemplatesService } from '../../../core/services/report-templates.service';
import { ReportTemplate, ReportSnapshot } from '../../../core/models/report.model';
import { ReportTemplateDialogComponent } from './report-template-dialog.component';
import { ReportScheduleDialogComponent } from './report-schedule-dialog.component';
import { ToasterService } from '../../../core/services/toaster.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-report-templates-list',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatChipsModule,
    MatDialogModule,
    MatMenuModule,
    MatTooltipModule
  ],
  templateUrl: './report-templates-list.component.html',
  styleUrls: ['./report-templates-list.component.scss']
})
export class ReportTemplatesListComponent extends BaseComponent implements OnInit {
  private templatesService = inject(ReportTemplatesService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  templates: ReportTemplate[] = [];
  displayedColumns = ['name', 'type', 'category', 'metrics', 'created', 'actions'];
  // loading inherited from BaseComponent

  ngOnInit(): void {
    this.loadTemplates();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.templatesService.getTemplates()
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (templates) => {
          this.templates = templates;
        },
        error: (err) => {
          console.error('Error loading templates:', err);
          this.toaster.alert('Şablonlar yüklenirken hata oluştu');
        }
      });
  }

  createTemplate(): void {
    const dialogRef = this.dialog.open(ReportTemplateDialogComponent, {
      width: '600px',
      data: null
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTemplates();
      }
    });
  }

  editTemplate(template: ReportTemplate): void {
    const dialogRef = this.dialog.open(ReportTemplateDialogComponent, {
      width: '600px',
      data: template
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTemplates();
      }
    });
  }

  scheduleReport(template: ReportTemplate): void {
    const dialogRef = this.dialog.open(ReportScheduleDialogComponent, {
      width: '600px',
      data: template
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.toaster.alert('Rapor zamanlaması oluşturuldu');
      }
    });
  }

  generateSnapshot(template: ReportTemplate): void {
    const newSnapshot: Omit<ReportSnapshot, "id" | "generatedAt"> = {
      reportType: template.reportType,
      templateId: template.id,
      data: {},
      generatedBy: ''
    };

    this.templatesService.createSnapshot(newSnapshot)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toaster.success('Snapshot başarıyla oluşturuldu');
          this.loadTemplates();
        },
        error: (error: any) => {
          console.error('Snapshot oluşturma hatası:', error);
          this.toaster.alert('Snapshot oluşturulurken bir hata oluştu');
        }
      });
  }

  deleteTemplate(template: ReportTemplate): void {
    if (confirm(`"${template.name}" şablonunu silmek istediğinizden emin misiniz?`)) {
      this.templatesService.deleteTemplate(template.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.toaster.alert('Şablon başarıyla silindi');
            this.loadTemplates();
          },
          error: (err) => {
            console.error('Error deleting template:', err);
            this.toaster.alert('Şablon silinirken hata oluştu');
          }
        });
    }
  }

  formatReportType(type: string): string {
    const typeMap: Record<string, string> = {
      'student-dashboard': 'Öğrenci Dashboard',
      'teacher-class': 'Öğretmen Sınıf',
      'admin-platform': 'Admin Platform'
    };
    return typeMap[type] || type;
  }

  getMetricsCount(template: ReportTemplate): number {
    if (!template.metrics || typeof template.metrics === 'string') {
      try {
        const metrics = JSON.parse(template.metrics as string);
        return Array.isArray(metrics) ? metrics.length : 0;
      } catch {
        return 0;
      }
    }
    return Array.isArray(template.metrics) ? template.metrics.length : 0;
  }
}
