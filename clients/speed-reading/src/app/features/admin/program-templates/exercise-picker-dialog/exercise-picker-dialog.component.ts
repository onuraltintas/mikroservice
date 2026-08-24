import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { Exercise, ExerciseType, ExerciseTypeCategory } from '../../../../core/models/exercise.model';
import { ExerciseTypeService } from '../../../../core/services/exercise-type.service';

@Component({
  selector: 'app-exercise-picker-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatTableModule,
    MatPaginatorModule, MatIconModule, MatChipsModule, MatButtonToggleModule,
    MatTooltipModule
  ],
  template: `
    <h2 mat-dialog-title>Egzersiz Seç</h2>
    <mat-dialog-content>
      <div class="filters">
        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>Tip</mat-label>
          <mat-select [(ngModel)]="selectedTypeId" (selectionChange)="onFilterChange()">
            <mat-option [value]="null">Tümü</mat-option>
            <mat-optgroup *ngFor="let group of groupedTypes" [label]="group.categoryName">
              <mat-option *ngFor="let type of group.types" [value]="type.id">
                <div class="option-content">
                  <span class="option-main">{{type.displayName || type.name}}</span>
                  <span class="option-subtitle" *ngIf="type.displayName && type.name">({{type.name}})</span>
                </div>
              </mat-option>
            </mat-optgroup>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>Zorluk</mat-label>
          <mat-select [(ngModel)]="selectedDifficulty" (selectionChange)="onFilterChange()">
            <mat-option [value]="null">Tümü</mat-option>
            <mat-option [value]="1">Seviye 1</mat-option>
            <mat-option [value]="2">Seviye 2</mat-option>
            <mat-option [value]="3">Seviye 3</mat-option>
            <mat-option [value]="4">Seviye 4</mat-option>
            <mat-option [value]="5">Seviye 5</mat-option>
          </mat-select>
        </mat-form-field>
        <div class="age-group-filter">
          <label>Yaş Grubu Filtresi:</label>
          <mat-button-toggle-group [value]="ageGroupFilter" (valueChange)="setAgeGroupFilter($event)">
            <mat-button-toggle value="selected" matTooltip="Seçili yaş grubuna özel + genel egzersizler">
              <mat-icon>group</mat-icon>
              Seçili Yaş Grubu
            </mat-button-toggle>
            <mat-button-toggle value="general" matTooltip="Sadece yaş grubu olmayan (genel) egzersizler">
              <mat-icon>public</mat-icon>
              Sadece Genel
            </mat-button-toggle>
            <mat-button-toggle value="all" matTooltip="Tüm egzersizler (filtresiz)">
              <mat-icon>select_all</mat-icon>
              Tümü
            </mat-button-toggle>
          </mat-button-toggle-group>
        </div>
      </div>

      <div class="table-container">
        <table mat-table [dataSource]="exercises" class="mat-elevation-z1">
            <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef> Başlık </th>
            <td mat-cell *matCellDef="let element"> {{element.title}} </td>
            </ng-container>

            <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef> Tip </th>
            <td mat-cell *matCellDef="let element">
              <div class="type-cell">
                <div class="type-main">{{element.exerciseTypeDisplayName || element.exerciseTypeName}}</div>
                <div class="type-subtitle" *ngIf="element.exerciseTypeDisplayName && element.exerciseTypeName">
                  {{element.exerciseTypeName}}
                </div>
              </div>
            </td>
            </ng-container>

            <ng-container matColumnDef="ageGroup">
            <th mat-header-cell *matHeaderCellDef> Yaş Grubu </th>
            <td mat-cell *matCellDef="let element"> 
                <span class="age-group-badge" [class]="'age-group-' + getAgeGroupClass(element.targetAgeGroupId)">
                  {{getAgeGroupLabel(element.targetAgeGroupId)}}
                </span>
            </td>
            </ng-container>

            <ng-container matColumnDef="difficulty">
            <th mat-header-cell *matHeaderCellDef> Zorluk </th>
            <td mat-cell *matCellDef="let element"> 
                <span class="difficulty-badge level-{{element.difficultyLevel}}">Seviye {{element.difficultyLevel}}</span>
            </td>
            </ng-container>

            <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef> </th>
            <td mat-cell *matCellDef="let element">
                <button mat-mini-fab color="primary" (click)="select(element)" matTooltip="Seç">
                <mat-icon>add</mat-icon>
                </button>
            </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </div>

      <mat-paginator [length]="totalCount" 
                     [pageSize]="pageSize" 
                     [pageSizeOptions]="[5, 10, 25]"
                     (page)="onPageChange($event)">
      </mat-paginator>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Kapat</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .filters { display: flex; gap: 12px; align-items: center; margin-top: 8px; margin-bottom: 12px; flex-wrap: wrap; }
    .filter-field { flex: 1; min-width: 180px; }
    .age-group-filter { display: flex; flex-direction: column; gap: 4px; }
    .age-group-filter label { font-size: 11px; font-weight: 500; color: rgba(0,0,0,0.6); margin: 0; }
    .age-group-filter mat-button-toggle-group { box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
    .table-container { max-height: calc(80vh - 180px); overflow: auto; margin-top: 8px; }
    table { width: 100%; }
    mat-paginator { margin-top: 8px; }
    
    .age-group-badge { 
      display: inline-block;
      padding: 3px 8px; 
      border-radius: 12px; 
      font-size: 10px; 
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .age-group-badge.age-group-child { background: #e3f2fd; color: #1565c0; }
    .age-group-badge.age-group-teen { background: #f3e5f5; color: #6a1b9a; }
    .age-group-badge.age-group-young-adult { background: #fff3e0; color: #e65100; }
    .age-group-badge.age-group-adult { background: #e8f5e9; color: #2e7d32; }
    .age-group-badge.age-group-general { background: #f5f5f5; color: #616161; }
    .age-group-badge.age-group-unknown { background: #ffebee; color: #c62828; }
    
    .type-cell {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    
    .type-main {
      font-size: 14px;
      font-weight: 500;
      color: #333;
    }
    
    .type-subtitle {
      font-size: 11px;
      font-style: italic;
      color: #999;
    }
    
    .option-content {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    
    .option-main {
      font-weight: 500;
    }
    
    .option-subtitle {
      font-size: 12px;
      font-style: italic;
      color: #999;
    }
    
    .difficulty-badge { padding: 3px 6px; border-radius: 3px; color: white; background: #999; font-size: 11px; }
    .difficulty-badge.level-1 { background: #4caf50; }
    .difficulty-badge.level-2 { background: #8bc34a; }
    .difficulty-badge.level-3 { background: #ffeb3b; color: black; }
    .difficulty-badge.level-4 { background: #ff9800; }
    .difficulty-badge.level-5 { background: #f44336; }
    mat-dialog-title { margin-bottom: 8px !important; padding: 16px 24px 8px !important; }
    mat-dialog-content { padding: 0 24px !important; }
    mat-dialog-actions { padding: 12px 24px !important; margin: 0 !important; }
  `]
})
export class ExercisePickerDialogComponent implements OnInit {
  exerciseService = inject(ExerciseService);
  exerciseTypeService = inject(ExerciseTypeService);
  dialogRef = inject(MatDialogRef<ExercisePickerDialogComponent>);

  // Injected Data
  targetAgeGroupId: string;
  originalAgeGroupId: string; // Store original value

  // Age group filter: 'selected' = specific age group, 'general' = null only, 'all' = everything
  ageGroupFilter: 'selected' | 'general' | 'all' = 'selected';

  exercises: Exercise[] = [];
  exerciseTypes: ExerciseType[] = [];
  categories: ExerciseTypeCategory[] = [];
  displayedColumns = ['title', 'type', 'ageGroup', 'difficulty', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  selectedTypeId: string | null = null;
  selectedDifficulty: number | null = null;

  // Grouped types by category
  groupedTypes: Array<{ categoryName: string, types: ExerciseType[] }> = [];

  // Known age group GUIDs
  private readonly AGE_GROUP_GUIDS = {
    CHILD: '10000000-0000-0000-0000-000000000001',
    TEEN: '10000000-0000-0000-0000-000000000002',
    ADULT: '10000000-0000-0000-0000-000000000003',
    YOUNG_ADULT: '10000000-0000-0000-0000-000000000004'
  };

  constructor(@Inject(MAT_DIALOG_DATA) public data: { targetAgeGroupId: string }) {
    this.targetAgeGroupId = data.targetAgeGroupId || '';
    this.originalAgeGroupId = data.targetAgeGroupId || '';



    // If no age group is selected, default to 'all' mode
    if (!this.targetAgeGroupId) {
      this.ageGroupFilter = 'all';
      console.warn('⚠️ No age group selected, defaulting to "all" mode');
    }
  }

  ngOnInit() {
    this.loadCategories();
    this.loadTypes();
    this.loadExercises();
  }

  loadCategories() {
    this.exerciseTypeService.getCategories().subscribe({
      next: (cats) => {
        this.categories = cats;
      },
      error: (err) => console.error('Failed to load categories', err)
    });
  }

  loadTypes() {
    this.exerciseTypeService.getActiveExerciseTypes().subscribe({
      next: (res) => {
        this.exerciseTypes = res.items;
        this.groupExerciseTypes();
      },
      error: (err) => console.error('Failed to load exercise types', err)
    });
  }

  groupExerciseTypes() {
    const grouped = new Map<string, ExerciseType[]>();

    // Initialize groups from categories
    this.categories.forEach(cat => {
      grouped.set(cat.displayName || cat.name, []);
    });

    // Add "Diğer" category
    grouped.set('Diğer', []);

    // Group types
    this.exerciseTypes.forEach(type => {
      const categoryName = type.categoryDisplayName || type.categoryName || 'Diğer';
      if (!grouped.has(categoryName)) {
        grouped.set(categoryName, []);
      }
      grouped.get(categoryName)!.push(type);
    });

    // Convert to array and filter empty groups
    this.groupedTypes = Array.from(grouped.entries())
      .map(([categoryName, types]) => ({ categoryName, types }))
      .filter(g => g.types.length > 0)
      .sort((a, b) => {
        // "Diğer" always last
        if (a.categoryName === 'Diğer') return 1;
        if (b.categoryName === 'Diğer') return -1;
        return a.categoryName.localeCompare(b.categoryName, 'tr');
      });
  }

  loadExercises() {
    // Determine which age group ID to send based on filter mode
    let ageGroupIdToSend: string | undefined;

    if (this.ageGroupFilter === 'selected') {
      // Send the selected age group ID (will match specific age group only)
      ageGroupIdToSend = this.targetAgeGroupId;
    } else if (this.ageGroupFilter === 'general') {
      // Send special GUID to indicate "only null" filter
      ageGroupIdToSend = '00000000-0000-0000-0000-000000000000';
    } else {
      // 'all' - don't send any filter
      ageGroupIdToSend = undefined;
    }



    this.exerciseService.getExercises(
      this.selectedTypeId || undefined,
      this.selectedDifficulty || undefined,
      ageGroupIdToSend,
      this.pageNumber,
      this.pageSize
    ).subscribe(res => {
      this.exercises = res.items;
      this.totalCount = res.totalCount;
    });
  }

  onFilterChange() {
    this.pageNumber = 1;
    this.loadExercises();
  }

  setAgeGroupFilter(filter: 'selected' | 'general' | 'all') {
    // Prevent undefined values
    if (!filter || (filter !== 'selected' && filter !== 'general' && filter !== 'all')) {
      console.warn('⚠️ Invalid filter value:', filter, '- keeping current:', this.ageGroupFilter);
      return;
    }

    this.ageGroupFilter = filter;
    this.pageNumber = 1;
    this.loadExercises();
  }

  getAgeGroupFilterLabel(): string {
    switch (this.ageGroupFilter) {
      case 'selected': return 'Seçili Yaş Grubu';
      case 'general': return 'Genel (Tüm Yaş Grupları)';
      case 'all': return 'Tümü';
      default: return '';
    }
  }

  onPageChange(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadExercises();
  }

  select(exercise: Exercise) {
    this.dialogRef.close(exercise);
  }

  getAgeGroupLabel(ageGroupId: string | null | undefined): string {
    if (!ageGroupId) return 'Genel';

    switch (ageGroupId) {
      case this.AGE_GROUP_GUIDS.CHILD:
        return 'Çocuk (9-12)';
      case this.AGE_GROUP_GUIDS.TEEN:
        return 'Genç (13-16)';
      case this.AGE_GROUP_GUIDS.YOUNG_ADULT:
        return 'G.Yetişkin (17-21)';
      case this.AGE_GROUP_GUIDS.ADULT:
        return 'Yetişkin (22+)';
      default:
        return 'Bilinmiyor';
    }
  }

  getAgeGroupClass(ageGroupId: string | null | undefined): string {
    if (!ageGroupId) return 'general';

    switch (ageGroupId) {
      case this.AGE_GROUP_GUIDS.CHILD:
        return 'child';
      case this.AGE_GROUP_GUIDS.TEEN:
        return 'teen';
      case this.AGE_GROUP_GUIDS.YOUNG_ADULT:
        return 'young-adult';
      case this.AGE_GROUP_GUIDS.ADULT:
        return 'adult';
      default:
        return 'unknown';
    }
  }

}
