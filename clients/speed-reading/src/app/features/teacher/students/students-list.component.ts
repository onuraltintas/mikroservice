import { Component, OnInit, inject, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToasterService } from '../../../core/services/toaster.service';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
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
import { StudentDialogComponent } from './student-dialog.component';
import { LinkStudentDialogComponent } from './link-student-dialog.component';
import { ConfirmationDialogComponent, ConfirmationDialogData } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { SubscriptionService } from '../../../core/services/subscription.service';

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
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);
  private subscriptionService = inject(SubscriptionService);
  private dialog = inject(MatDialog);
  protected override toaster = inject(ToasterService);

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

  activeAccessStudentIds = new Set<string>();
  suspendedAccessStudentIds = new Set<string>();
  selectedTeacherUserId?: string;
  pageIndex = 0;
  pageSize = 10;
  totalStudents = 0;

  ngOnInit() {
    this.selectedTeacherUserId = this.route.snapshot.queryParamMap.get('teacherId') ?? undefined;
    this.setupFilters();

    // Check role and update columns
    const isInstitutionAdmin = this.isInstitutionAdmin();
    if (isInstitutionAdmin) {
      // Insert 'teacher' column after 'name'
      this.displayedColumns = ['avatar', 'name', 'teacher', 'access', 'level', 'target', 'dailyGoal', 'lastLogin', 'status', 'actions'];
      this.loadInstitutionAccess();
    }

    this.refreshData(); // Initial load
  }

  loadInstitutionAccess() {
    this.subscriptionService.getMyInstitutionAccess()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: license => {
        this.activeAccessStudentIds = new Set(license?.activeStudentIds ?? []);
        this.suspendedAccessStudentIds = new Set(license?.suspendedStudentIds ?? []);
      }});
  }

  hasInstitutionAccess(student: Student): boolean {
    return this.activeAccessStudentIds.has(student.id);
  }

  isInstitutionAccessSuspended(student: Student): boolean {
    return this.suspendedAccessStudentIds.has(student.id);
  }

  isInstitutionAdmin(): boolean {
    return this.authService.hasRole('InstitutionAdmin')
      || this.authService.hasRole('InstitutionOwner');
  }

  changeInstitutionAccess(student: Student, isSuspended: boolean): void {
    if (!this.isInstitutionAdmin()) return;
    const action = isSuspended ? 'duraklatmak' : 'yeniden açmak';
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: isSuspended ? 'Hızlı okuma erişimini duraklat' : 'Hızlı okuma erişimini yeniden aç',
        message: `${student.firstName} ${student.lastName} için hızlı okuma erişimini ${action} istiyor musunuz? İşlem kayıt altına alınır.`,
        confirmText: isSuspended ? 'Duraklat' : 'Erişimi aç',
        cancelText: 'İptal'
      } as ConfirmationDialogData
    });
    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.loading.set(true);
      this.subscriptionService.changeMyInstitutionStudentAccess(
        student.id,
        isSuspended,
        `Kurum yöneticisi tarafından ${isSuspended ? 'duraklatıldı' : 'yeniden açıldı'}.`
      ).pipe(finalize(() => this.loading.set(false))).subscribe({
        next: () => {
          this.toaster.success(isSuspended ? 'Hızlı okuma erişimi duraklatıldı.' : 'Hızlı okuma erişimi yeniden açıldı.');
          this.loadInstitutionAccess();
        },
        error: error => this.handleError(error, 'Öğrenci erişim durumu güncellenemedi')
      });
    });
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
      .subscribe(() => this.refreshData(true));

    this.levelControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshData(true));

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshData(true));
  }

  /**
   * Unified method to load data based on role and filters.
   * Replaces both initializeTeacherData and applyFilters.
   */
  refreshData(resetPage = false) {
    if (resetPage) this.pageIndex = 0;
    const user = this.authService.currentUserValue;
    if (user && this.isInstitutionAdmin()) {
      // Institution admins can filter the full institution roster.
      const searchTerm = this.searchControl.value || '';
      const level = this.levelControl.value;
      const status = this.statusControl.value;

      this.loadStudentsAdmin(searchTerm, level ?? undefined, status ?? undefined);
    } else {
      this.loadStudents();
    }
  }

  loadStudents() {
    this.loading.set(true);
    this.teachersService.getMyStudentsPage(
      this.pageIndex + 1,
      this.pageSize,
      this.searchControl.value || undefined,
      this.levelControl.value ?? undefined,
      this.statusControl.value ?? undefined)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (page) => {
          this.dataSource.data = page.items;
          this.totalStudents = page.totalCount;
        },
        error: (error) => {
          this.handleError(error, 'Öğrenciler yüklenirken hata oluştu');
        }
      });
  }

  loadStudentsAdmin(searchTerm: string = '', level?: number, isActive?: boolean) {
    this.loading.set(true);
    this.studentsService.getInstitutionStudentsPage(
      this.pageIndex + 1,
      this.pageSize,
      searchTerm,
      level,
      isActive,
      this.selectedTeacherUserId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (page) => {
          this.dataSource.data = page.items;
          this.totalStudents = page.totalCount;
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

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.refreshData();
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
    if (!this.isInstitutionAdmin()) return;

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
    const isInstitutionAdmin = this.isInstitutionAdmin();

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
