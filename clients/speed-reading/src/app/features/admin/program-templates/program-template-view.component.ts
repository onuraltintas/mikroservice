import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatTabsModule } from '@angular/material/tabs';
import {
  ProgramTemplateService,
  ExerciseProgramTemplate,
  WeeklyPattern,
  ExercisePattern
} from '../../../core/services/program-template.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

/**
 * Program Template View Component
 * Display detailed view of exercise program template
 */
@Component({
  selector: 'app-program-template-view',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatTabsModule
  ],
  templateUrl: './program-template-view.component.html',
  styleUrls: ['./program-template-view.component.scss']
})
export class ProgramTemplateViewComponent extends BaseComponent implements OnInit {
  readonly templateService = inject(ProgramTemplateService);
  // toaster inherited from BaseComponent
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  template = signal<ExerciseProgramTemplate | null>(null);
  weeklyPattern = signal<WeeklyPattern | null>(null);
  weekKeys = computed(() => {
    const pattern = this.weeklyPattern();
    return pattern ? this.templateService.getWeekKeys(pattern) : [];
  });
  studentCount = signal<number>(0);
  // loading inherited from BaseComponent

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.handleError(new Error('Geçersiz şablon ID'), 'Geçersiz şablon ID');
      this.goBack();
      return;
    }

    this.loadTemplate(id);
    this.loadStudentCount(id);
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadTemplate(id: string): void {
    this.templateService.getById(id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (template) => {
          this.template.set(template);

          // Parse weekly pattern
          const pattern = this.templateService.parseWeeklyPattern(template.weeklyPatternJson);

          if (!pattern) {
            this.toaster.warning('Haftalık pattern parse edilemedi. Raw JSON\'u kontrol edin.');
          }

          this.weeklyPattern.set(pattern);
        },
        error: (err) => {
          this.handleError(err, 'Şablon yüklenirken hata oluştu');
          this.goBack();
        }
      });
  }

  loadStudentCount(id: string): void {
    this.templateService.getStudentCount(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (count) => this.studentCount.set(count),
        error: () => { }
      });
  }

  getAgeGroupName(): string {
    return this.template()?.targetAgeGroupName || 'Bilinmeyen';
  }

  // getAgeGroupClass removed


  editTemplate(): void {
    const id = this.template()?.id;
    if (id) {
      this.router.navigate(['/admin/program-templates', id, 'edit']);
    }
  }

  async deleteTemplate() {
    const template = this.template();
    if (!template) return;

    if (template.isActive) {
      this.toaster.warning('Aktif şablonlar silinemez. Önce pasif yapın.');
      return;
    }

    if (this.studentCount() > 0) {
      this.toaster.warning('Bu şablona atanmış öğrenciler var, silinemez.');
      return;
    }

    const confirmed = await this.confirm(`"${template.name}" şablonunu silmek istediğinize emin misiniz?`);
    if (!confirmed) {
      return;
    }

    this.templateService.delete(template.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.handleSuccess('Şablon silindi');
          this.goBack();
        },
        error: (err) => {
          this.handleError(err, 'Silme işlemi başarısız');
        }
      });
  }

  goBack(): void {
    this.router.navigate(['/admin/program-templates']);
  }
}
