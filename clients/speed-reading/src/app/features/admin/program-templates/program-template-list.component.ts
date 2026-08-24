import { Component, OnInit, inject, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import {
  ProgramTemplateService,
  ExerciseProgramTemplate,
  ProgramType
} from '../../../core/services/program-template.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

/**
 * Program Template List Component
 * Shows all exercise program templates with CRUD actions
 */
@Component({
  selector: 'app-program-template-list',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatChipsModule,
    MatTooltipModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatPaginatorModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    FormsModule
  ],
  templateUrl: './program-template-list.component.html',
  styleUrls: ['./program-template-list.component.scss']
})
export class ProgramTemplateListComponent extends BaseComponent implements OnInit {
  private readonly templateService = inject(ProgramTemplateService);
  private readonly ageGroupService = inject(AgeGroupConfigurationService);
  // toaster inherited from BaseComponent
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  // Paginator and Sort - using setters for *ngIf compatibility
  private _paginator!: MatPaginator;
  private _sort!: MatSort;

  @ViewChild(MatPaginator) set paginator(p: MatPaginator) {
    this._paginator = p;
    if (this._paginator) {
      this.dataSource.paginator = this._paginator;
    }
  }

  @ViewChild(MatSort) set sort(s: MatSort) {
    this._sort = s;
    if (this._sort) {
      this.dataSource.sort = this._sort;
    }
  }

  templates = signal<ExerciseProgramTemplate[]>([]);
  dataSource = new MatTableDataSource<ExerciseProgramTemplate>([]);
  override loading = signal<boolean>(true);
  error = signal<string | null>(null);
  updatingStatus = new Set<string>();

  displayedColumns: string[] = [
    'name',
    'ageGroup',
    'scoreRange',
    'difficulty',
    'studentCount',
    'status',
    'actions'
  ];

  ageGroups: AgeGroupConfiguration[] = [];
  searchText = '';
  selectedDifficulty: number | null = null;
  selectedAgeGroupId: string | null = null;

  getProgramTypeName(programType: ProgramType): string {
    return programType === ProgramType.ExamPrep ? 'Sınav Hazırlık' : 'Standart Program';
  }

  private studentCounts = new Map<string, number>();

  ngOnInit(): void {
    this.loadAgeGroups();
    this.loadTemplates();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.error.set(null);

    this.templateService.getAll()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (data) => {
          const filteredData = data.filter(t => !t.isDeleted);
          this.templates.set(filteredData);
          this.applyFilters();

          // Load student counts for each template
          filteredData.forEach(template => {
            this.templateService.getStudentCount(template.id)
              .pipe(takeUntil(this.destroy$))
              .subscribe({
                next: (count) => this.studentCounts.set(template.id, count),
                error: () => {
                  // Silently fail for student count - not critical
                  this.studentCounts.set(template.id, 0);
                }
              });
          });
        },
        error: (err) => {
          console.error('Program şablonları yüklenirken hata:', err);
          this.error.set('Program şablonları yüklenirken hata oluştu. Lütfen backend API\'yi kontrol edin.');
        }
      });
  }

  loadAgeGroups() {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.ageGroups = groups.sort((a, b) => a.orderIndex - b.orderIndex);
        },
        error: (error) => {
          console.error('Yaş grupları yüklenirken hata:', error);
        }
      });
  }

  applyFilters() {
    let filtered = this.templates();

    // 1. Search Query
    if (this.searchText && this.searchText.trim()) {
      const query = this.searchText.toLowerCase().trim();
      filtered = filtered.filter(t =>
        t.name.toLowerCase().includes(query) ||
        t.description?.toLowerCase().includes(query)
      );
    }

    // 2. Age Group Filter
    if (this.selectedAgeGroupId) {
      filtered = filtered.filter(t => t.targetAgeGroupId === this.selectedAgeGroupId);
    }

    // 3. Difficulty Filter (Initial Difficulty)
    if (this.selectedDifficulty) {
      filtered = filtered.filter(t => t.initialDifficultyLevel === this.selectedDifficulty);
    }

    this.dataSource.data = filtered;

    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  clearFilters() {
    this.searchText = '';
    this.selectedAgeGroupId = null;
    this.selectedDifficulty = null;
    this.applyFilters();
  }

  getAgeGroupName(template: ExerciseProgramTemplate): string {
    return template.targetAgeGroupName || 'Bilinmeyen';
  }

  getAgeGroupLabel(group: AgeGroupConfiguration): string {
    return `${group.displayName || group.name} (${this.ageGroupService.formatAgeRange(group)})`;
  }

  getStudentCount(templateId: string): number | null {
    return this.studentCounts.get(templateId) ?? null;
  }

  createTemplate(): void {
    this.router.navigate(['/admin/program-templates/create']);
  }

  viewTemplate(template: ExerciseProgramTemplate): void {
    this.router.navigate(['/admin/program-templates', template.id, 'view']);
  }

  editTemplate(template: ExerciseProgramTemplate): void {
    this.router.navigate(['/admin/program-templates', template.id, 'edit']);
  }

  async cloneTemplate(template: ExerciseProgramTemplate): Promise<void> {
    const confirmed = await this.confirm(
      `"${template.name}" şablonunu kopyalamak istiyor musunuz?`
    );

    if (!confirmed) return;

    this.loading.set(true);
    this.templateService.clone(template.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (clonedTemplate) => {
          this.handleSuccess('Şablon başarıyla kopyalandı.');
          this.loadTemplates();
        },
        error: (err) => {
          this.handleError(err, 'Şablon kopyalanırken bir hata oluştu.');
        }
      });
  }

  toggleStatus(template: ExerciseProgramTemplate, isActive: boolean): void {
    this.updatingStatus.add(template.id);

    const request = {
      name: template.name,
      description: template.description,
      targetAgeGroupId: template.targetAgeGroupId,
      minAssessmentScore: template.minAssessmentScore,
      maxAssessmentScore: template.maxAssessmentScore,
      weeklyPatternJson: template.weeklyPatternJson,
      initialDifficultyLevel: template.initialDifficultyLevel,
      weeksPerDifficultyIncrease: template.weeksPerDifficultyIncrease,
      maxDifficultyLevel: template.maxDifficultyLevel,
      programType: template.programType,
      examType: template.examType,
      displayOrder: template.displayOrder,
      isActive: isActive
    };

    this.templateService.update(template.id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          template.isActive = isActive;
          this.updatingStatus.delete(template.id);
          this.handleSuccess(
            `Şablon ${isActive ? 'aktif' : 'pasif'} hale getirildi`
          );
        },
        error: (err) => {
          this.updatingStatus.delete(template.id);
          this.handleError(err, 'Durum güncellenirken hata oluştu');
          // Revert toggle if failed
          template.isActive = !isActive;
        }
      });
  }

  async deleteTemplate(template: ExerciseProgramTemplate) {
    if (template.isActive) {
      this.handleError(new Error('Aktif şablon silinemez'), 'Aktif şablon silinemez. Önce pasif hale getirin.');
      return;
    }

    const studentCount = this.studentCounts.get(template.id) || 0;
    if (studentCount > 0) {
      this.handleError(new Error('Kullanımda olan şablon silinemez'), `Bu şablonu kullanan ${studentCount} öğrenci var. Şablon silinemez.`);
      return;
    }

    const confirmed = await this.confirm(`"${template.name}" şablonunu silmek istediğinizden emin misiniz?`);
    if (!confirmed) {
      return;
    }

    this.templateService.delete(template.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.handleSuccess('Şablon silindi');
          this.loadTemplates();
        },
        error: (err) => {
          this.handleError(err, 'Şablon silinirken hata oluştu');
        }
      });
  }
}
