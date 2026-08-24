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
import { TeachersService } from '../../../core/services/teachers.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { Teacher } from '../../../core/models/teacher.model';
import { Institution } from '../../../core/models/institution.model';
import { TeacherDialogComponent } from './teacher-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-teachers-list',
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
  templateUrl: './teachers-list.component.html',
  styleUrls: ['./teachers-list.component.scss']
})
export class TeachersListComponent extends BaseComponent implements OnInit, AfterViewInit {
  private teachersService = inject(TeachersService);
  private institutionsService = inject(InstitutionsService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new MatTableDataSource<Teacher>([]);
  displayedColumns = ['index', 'avatar', 'name', 'email', 'institution', 'studentCount', 'lastLoginAt', 'status', 'actions'];
  // loading inherited from BaseComponent

  searchControl = new FormControl('');
  institutionControl = new FormControl<string | null>(null);
  statusControl = new FormControl<boolean | null>(null);

  institutions: Institution[] = [];

  ngOnInit() {
    this.loadInstitutions();
    this.setupFilters();
    this.loadTeachers();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: Teacher, filter: string) => {
      const searchTerm = filter.toLowerCase();
      return data.firstName.toLowerCase().includes(searchTerm) ||
        data.lastName.toLowerCase().includes(searchTerm) ||
        data.email.toLowerCase().includes(searchTerm);
    };
  }

  loadInstitutions() {
    this.institutionsService.getInstitutions(undefined, true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (institutions) => {
          this.institutions = institutions;
        },
        error: (error) => {
          this.toaster.error('Kurumlar yüklenirken bir hata oluştu');
        }
      });
  }

  setupFilters() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.applyFilters());

    this.institutionControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());
  }

  applyFilters() {
    const searchTerm = this.searchControl.value || '';
    const institutionId = this.institutionControl.value || undefined;
    const status = this.statusControl.value;

    this.loadTeachers(searchTerm, institutionId, status ?? undefined);
  }

  loadTeachers(searchTerm?: string, institutionId?: string, isActive?: boolean) {
    this.loading.set(true);
    this.teachersService.getTeachers(searchTerm, institutionId, isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (teachers) => {
          this.dataSource.data = teachers;
        },
        error: (error) => {
          this.toaster.error('Öğretmenler yüklenirken bir hata oluştu');
        }
      });
  }

  openDialog(teacher?: Teacher) {
    const dialogRef = this.dialog.open(TeacherDialogComponent, {
      width: '600px',
      data: teacher,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  async toggleTeacherStatus(teacher: Teacher) {
    const action = teacher.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.toaster.confirm(`${teacher.firstName} ${teacher.lastName} ${action} mı?`);

    if (confirmed) {
      const updateRequest = {
        id: teacher.id,
        firstName: teacher.firstName,
        lastName: teacher.lastName,
        email: teacher.email,
        institutionId: teacher.institutionId,
        isActive: !teacher.isActive
      };

      this.teachersService.updateTeacher(teacher.id, updateRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.toaster.success(
              teacher.isActive ? 'Öğretmen devre dışı bırakıldı' : 'Öğretmen aktif edildi',
              3000
            );
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('İşlem başarısız oldu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  async deleteTeacher(teacher: Teacher) {
    const confirmed = await this.toaster.confirm(
      `"${teacher.firstName} ${teacher.lastName}" öğretmenini silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`
    );

    if (confirmed) {
      this.teachersService.deleteTeacher(teacher.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.toaster.success('Öğretmen silindi', 3000);
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('Öğretmen silinirken bir hata oluştu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
  }

  getAvatarBackground(teacher: Teacher): string {
    const colors = [
      'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      'linear-gradient(135deg, #2af598 0%, #009efd 100%)',
      'linear-gradient(135deg, #ff9a9e 0%, #fecfef 99%, #fecfef 100%)',
      'linear-gradient(135deg, #f6d365 0%, #fda085 100%)',
      'linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%)',
      'linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)'
    ];
    const index = (teacher.firstName.length + teacher.lastName.length) % colors.length;
    return colors[index];
  }
}
