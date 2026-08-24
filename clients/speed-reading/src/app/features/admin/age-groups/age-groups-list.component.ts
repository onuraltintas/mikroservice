import { Component, OnInit, inject, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { AgeGroupDialogComponent } from './age-group-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-age-groups-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatChipsModule
  ],
  templateUrl: './age-groups-list.component.html',
  styleUrls: ['./age-groups-list.component.scss']
})
export class AgeGroupsListComponent extends BaseComponent implements OnInit, AfterViewInit {
  private ageGroupService = inject(AgeGroupConfigurationService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new MatTableDataSource<AgeGroupConfiguration>([]);
  displayedColumns = ['index', 'displayName', 'ageRange', 'recommendedWPM', 'recommendedComprehension',
    'recommendedDailyMinutes', 'defaultDifficultyLevel', 'orderIndex', 'status', 'actions'];
  // loading inherited from BaseComponent

  searchControl = new FormControl('');
  statusControl = new FormControl<boolean | null>(null);

  ngOnInit() {
    this.setupFilters();
    this.loadAgeGroups();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: AgeGroupConfiguration, filter: string) => {
      const searchTerm = filter.toLowerCase();
      return data.name.toLowerCase().includes(searchTerm) ||
        data.displayName.toLowerCase().includes(searchTerm);
    };
  }

  setupFilters() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.applyFilters());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());
  }

  applyFilters() {
    const searchTerm = this.searchControl.value || '';
    this.dataSource.filter = searchTerm.trim().toLowerCase();

    const status = this.statusControl.value;
    if (status !== null) {
      this.loadAgeGroups(status);
    } else {
      this.loadAgeGroups();
    }
  }

  loadAgeGroups(activeOnly?: boolean) {
    this.loading.set(true);
    this.ageGroupService.getAll(activeOnly)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (groups) => {
          this.dataSource.data = groups;
        },
        error: (error) => {
          this.handleError(error, 'Yaş grupları yüklenirken hata oluştu');
        }
      });
  }

  openDialog(ageGroup?: AgeGroupConfiguration) {
    const dialogRef = this.dialog.open(AgeGroupDialogComponent, {
      width: '820px',
      maxWidth: '95vw',
      data: ageGroup,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  async deleteAgeGroup(group: AgeGroupConfiguration) {
    const confirmed = await this.confirm(
      `"${group.displayName}" yaş grubunu silmek istediğinizden emin misiniz?`
    );

    if (confirmed) {
      this.ageGroupService.delete(group.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Yaş grubu silindi');
            this.applyFilters();
          },
          error: (error) => {
            this.handleError(error, 'Yaş grubu silinirken hata oluştu');
          }
        });
    }
  }
}
