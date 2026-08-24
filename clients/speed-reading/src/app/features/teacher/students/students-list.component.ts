import { Component, OnInit, inject, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToasterService } from '../../../core/services/toaster.service';
import { RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';
import { StudentsService } from '../../../core/services/students.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { AuthService } from '../../../core/services/auth.service';
import { Student } from '../../../core/models/student.model';
import { BaseComponent } from '../../../core/components/base.component';
import { AdminContextService } from '../../../core/services/admin-context.service';
import { StudentDialogComponent } from './student-dialog.component';
import { LinkStudentDialogComponent } from './link-student-dialog.component';
import { ConfirmationDialogComponent, ConfirmationDialogData } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-teacher-students-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule,
    MatDialogModule
  ],
  templateUrl: './students-list.component.html',
  styleUrls: ['./students-list.component.scss']
})
export class StudentsListComponent extends BaseComponent implements OnInit, AfterViewInit, OnDestroy {
  private studentsService = inject(StudentsService);
  private teachersService = inject(TeachersService);
  private authService = inject(AuthService);
  private adminContext = inject(AdminContextService);
  private dialog = inject(MatDialog);
  protected override toaster = inject(ToasterService);

  @ViewChild(MatPaginator) set paginator(value: MatPaginator) {
    if (value) {
      this.dataSource.paginator = value;
    }
  }
  @ViewChild(MatSort) set sort(value: MatSort) {
    if (value) {
      this.dataSource.sort = value;
    }
  }

  dataSource = new MatTableDataSource<Student>([]);
  displayedColumns = ['avatar', 'name', 'level', 'target', 'dailyGoal', 'lastLogin', 'status', 'actions'];

  searchControl = new FormControl('');
  levelControl = new FormControl<number | null>(null);
  statusControl = new FormControl<boolean | null>(null);

  currentInstitutionId?: string;

  ngOnInit() {
    this.setupFilters();

    // Check role and update columns
    const isInstitutionAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');
    if (isInstitutionAdmin) {
      // Insert 'teacher' column after 'name'
      this.displayedColumns = ['avatar', 'name', 'teacher', 'level', 'target', 'dailyGoal', 'lastLogin', 'status', 'actions'];
    }

    this.refreshData(); // Initial load
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    // Paginator and Sort are handled via setters to support *ngIf usage

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: Student, filter: string) => {
      const searchTerm = filter.toLowerCase();
      const fullName = `${data.firstName} ${data.lastName}`.toLowerCase();
      return fullName.includes(searchTerm) || data.email.toLowerCase().includes(searchTerm);
    };
  }

  setupFilters() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.refreshData());

    this.levelControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshData());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshData());
  }

  /**
   * Unified method to load data based on role and filters.
   * Replaces both initializeTeacherData and applyFilters.
   */
  refreshData() {
    const user = this.authService.currentUserValue;
    if (user && (this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin'))) {
      // Admin/InstitutionAdmin: Use admin endpoint with query params
      const searchTerm = this.searchControl.value || '';
      const level = this.levelControl.value;
      const status = this.statusControl.value;

      // Check for impersonation context
      if (this.adminContext.isImpersonatingInstitution()) {
        this.currentInstitutionId = this.adminContext.institutionId()!;
      }

      this.loadStudentsAdmin(searchTerm, level ?? undefined, status ?? undefined);
    } else {
      // Teacher: Use teacher endpoint (backend handles filtering by teacher's institution/assignments)
      // Note: Client-side filtering is applied via dataSource.filter in loadStudents callback
      this.loadStudents();
    }
  }

  loadStudents() {
    this.loading.set(true);
    this.teachersService.getMyStudents()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (students) => {
          this.dataSource.data = students;
          const searchTerm = this.searchControl.value || '';
          if (searchTerm) {
            this.dataSource.filter = searchTerm.trim().toLowerCase();
          }
        },
        error: (error) => {
          this.handleError(error, 'Öğrenciler yüklenirken hata oluştu');
        }
      });
  }

  loadStudentsAdmin(searchTerm: string = '', level?: number, isActive?: boolean) {
    this.loading.set(true);
    this.studentsService.getStudents(searchTerm, this.currentInstitutionId, level, isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (students) => {
          this.dataSource.data = students;
        },
        error: (error) => {
          this.handleError(error, 'Öğrenciler yüklenirken hata oluştu');
        }
      });
  }

  clearFilters() {
    this.searchControl.setValue('');
    this.levelControl.setValue(null);
    this.statusControl.setValue(null);
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
  }

  getAvatarBackground(student: Student): string {
    const colors = [
      'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      'linear-gradient(135deg, #2af598 0%, #009efd 100%)',
      'linear-gradient(135deg, #ff9a9e 0%, #fecfef 99%, #fecfef 100%)',
      'linear-gradient(135deg, #f6d365 0%, #fda085 100%)',
      'linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%)',
      'linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)'
    ];
    const index = (student.firstName.length + student.lastName.length) % colors.length;
    return colors[index];
  }

  openStudentDialog(student?: Student): void {
    const dialogRef = this.dialog.open(StudentDialogComponent, {
      width: '500px',
      data: { student }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshData(); // Changed from loadStudents() to refreshData()
      }
    });
  }

  deleteStudent(student: Student): void {
    const isInstitutionAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');

    const dialogData: ConfirmationDialogData = {
      title: isInstitutionAdmin ? 'Öğrenciyi Kurumdan Çıkar' : 'Öğrenciyi Sınıftan Çıkar',
      message: isInstitutionAdmin
        ? `${student.firstName} ${student.lastName} isimli öğrenciyi kurumdan çıkarmak istediğinize emin misiniz?\n\nNot: Öğrenci sistemden TAMAMEN SİLİNMEYECEK, sadece kurumunuzla bağlantısı kesilerek "boşa" düşecektir.`
        : `${student.firstName} ${student.lastName} isimli öğrenciyi sınıfınızdan çıkarmak istediğinize emin misiniz?\n\nNot: Öğrenci silinmeyecek, sadece sizin listenizden kaldırılacaktır.`,
      confirmText: 'Çıkar',
      cancelText: 'İptal'
    };

    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loading.set(true);
        if (isInstitutionAdmin) {
          this.studentsService.unlinkStudentFromInstitution(student.id).subscribe({
            next: () => {
              this.toaster.success('Öğrenci kurumdan çıkarıldı.');
              this.refreshData();
              this.loading.set(false);
            },
            error: (err) => {
              this.loading.set(false);
              this.toaster.error('Öğrenci çıkarılırken bir hata oluştu. Lütfen tekrar deneyin.');
            }
          });
        } else {
          this.teachersService.unlinkStudent(student.id).subscribe({
            next: () => {
              this.toaster.success('Öğrenci sınıfınızdan çıkarıldı.');
              this.refreshData();
              this.loading.set(false);
            },
            error: (err) => {
              this.loading.set(false);
              this.toaster.error('Öğrenci çıkarılırken bir hata oluştu. Lütfen tekrar deneyin.');
            }
          });
        }
      }
    });
  }

  onFileSelected(event: any): void {
    const file: File = event.target.files[0];
    if (file) {
      this.loading.set(true);
      const isInstitutionAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');

      const request$ = isInstitutionAdmin
        ? this.studentsService.importStudents(file)
        : this.teachersService.importStudents(file);

      request$.subscribe({
        next: (result) => {
          this.loading.set(false);
          if (result.failureCount > 0) {
            this.toaster.warning(`${result.successCount} öğrenci eklendi, ${result.failureCount} hata oluştu. Hatalar: \n${result.errors.join('\n')}`);
          } else {
            this.toaster.success(`${result.successCount} öğrenci başarıyla eklendi.`);
          }
          this.refreshData();
        },
        error: (err) => {
          this.loading.set(false);
          this.toaster.error('Dosya yüklenirken bir hata oluştu. Lütfen geçerli bir dosya olduğundan emin olun.');
        }
      });
    }
  }

  downloadTemplate(): void {
    const isInstitutionAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');
    const request$ = isInstitutionAdmin
      ? this.studentsService.getImportTemplate()
      : this.teachersService.getImportTemplate();

    request$.subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = isInstitutionAdmin ? 'Ogrenci_Yukleme_Sablonu_Kurum.xlsx' : 'Ogrenci_Yukleme_Sablonu.xlsx';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.toaster.error('Şablon indirilemedi.');
      }
    });
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }

  linkStudent(): void {
    const dialogRef = this.dialog.open(LinkStudentDialogComponent, {
      width: '450px'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshData(); // Changed from loadStudents() to refreshData()
      }
    });
  }
}
