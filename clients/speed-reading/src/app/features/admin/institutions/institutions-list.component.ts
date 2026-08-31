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
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { Institution } from '../../../core/models/institution.model';
import { InstitutionDialogComponent } from './institution-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-institutions-list',
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
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './institutions-list.component.html',
  styleUrls: ['./institutions-list.component.scss']
})
export class InstitutionsListComponent extends BaseComponent implements OnInit, AfterViewInit {
  private institutionsService = inject(InstitutionsService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new MatTableDataSource<Institution>([]);
  displayedColumns = ['index', 'code', 'name', 'email', 'teacherCount', 'studentCount', 'status', 'createdAt', 'actions'];
  // loading inherited from BaseComponent

  searchControl = new FormControl('');
  statusControl = new FormControl<boolean | null>(null);

  ngOnInit() {
    this.setupFilters();
    this.loadInstitutions();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: Institution, filter: string) => {
      const searchTerm = filter.toLowerCase();
      return data.name.toLowerCase().includes(searchTerm) ||
        data.contactEmail.toLowerCase().includes(searchTerm);
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
    const status = this.statusControl.value;

    this.loadInstitutions(searchTerm, status ?? undefined);
  }

  loadInstitutions(searchTerm?: string, isActive?: boolean) {
    this.loading.set(true);
    this.institutionsService.getInstitutions(searchTerm, isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (institutions) => {
          this.dataSource.data = institutions;
        },
        error: (error) => {
          this.toaster.error('Kurumlar yüklenirken bir hata oluştu');
        }
      });
  }

  openDialog(institution?: Institution) {
    const dialogRef = this.dialog.open(InstitutionDialogComponent, {
      width: '900px',
      maxWidth: '95vw',
      data: institution,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  async toggleInstitutionStatus(institution: Institution) {
    const action = institution.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.confirm(`${institution.name} kurumu ${action} mı?`);
    if (confirmed) {
      this.institutionsService.setActive(institution.id, !institution.isActive)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess(
              institution.isActive ? 'Kurum devre dışı bırakıldı' : 'Kurum aktif edildi'
            );
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('İşlem başarısız oldu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  async deleteInstitution(institution: Institution) {
    const confirmed = await this.confirm(`"${institution.name}" kurumunu silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`);
    if (confirmed) {
      this.institutionsService.deleteInstitution(institution.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Kurum silindi');
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('Kurum silinirken bir hata oluştu. Lütfen bağlantılı verileri kontrol edin.');
          }
        });
    }
  }
}
