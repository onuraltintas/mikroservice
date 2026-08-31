import { Component, OnInit, inject, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router'; // Added import
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { NgxMatSelectSearchModule } from 'ngx-mat-select-search';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { UsersService } from '../../../core/services/users.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { Student } from '../../../core/models/student.model';
import { Institution } from '../../../core/models/institution.model';
import { Teacher } from '../../../core/models/teacher.model';
import { StudentDialogComponent } from './student-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-students-list',
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
    MatDividerModule,
    NgxMatSelectSearchModule
  ],
  templateUrl: './students-list.component.html',
  styleUrls: ['./students-list.component.scss']
})
export class StudentsListComponent extends BaseComponent implements OnInit, AfterViewInit {
  private studentsService = inject(StudentsService);
  private institutionsService = inject(InstitutionsService);
  private usersService = inject(UsersService);
  private dialog = inject(MatDialog);
  private router = inject(Router); // Injected Router
  // toaster is inherited from BaseComponent

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new MatTableDataSource<Student>([]);
  displayedColumns = ['index', 'name', 'email', 'institution', 'teacher', 'currentLevel', 'targetWPM', 'lastLoginAt', 'actions'];
  // loading inherited from BaseComponent

  searchControl = new FormControl('');
  institutionControl = new FormControl<string | null>(null);
  levelControl = new FormControl<number | null>(null);
  teacherControl = new FormControl<string | null>(null);

  institutions: Institution[] = [];
  filteredInstitutions: Institution[] = [];
  teachers: Teacher[] = [];
  filteredTeachers: Teacher[] = [];

  // Filter controls for search inside select
  instFilterCtrl = new FormControl('');
  teacherFilterCtrl = new FormControl('');

  ngOnInit() {
    this.loadInstitutions();
    this.loadTeachers(); // Load all teachers initially
    this.setupFilters();
    this.loadStudents();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: Student, filter: string) => {
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
          this.filteredInstitutions = institutions; // Initialize filtered list
        },
        error: (error) => {
          this.toaster.error('Kurumlar yüklenirken bir hata oluştu');
        }
      });
  }

  loadTeachers(institutionId?: string) {
    this.usersService.getUsers(undefined, 'Teacher', true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (teachers) => {
          const filteredTeachers = (institutionId
            ? teachers.filter(teacher => teacher.institutionId === institutionId)
            : teachers).map(teacher => ({
              id: teacher.id,
              firstName: teacher.firstName,
              lastName: teacher.lastName,
              email: teacher.email,
              institutionId: teacher.institutionId,
              institutionName: teacher.institutionName,
              studentCount: 0,
              lastLoginAt: teacher.lastLoginAt,
              isActive: teacher.isActive,
              createdAt: teacher.createdAt ?? new Date(0)
            }));
          this.teachers = filteredTeachers;
          this.filteredTeachers = this.teachers; // Initialize filtered list
        },
        error: (error) => {
          console.error('Error loading teachers:', error);
          this.toaster.error('Öğretmenler yüklenirken bir hata oluştu');
        }
      });
  }

  setupFilters() {
    // Main filters
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(() => this.applyFilters());

    this.institutionControl.valueChanges.subscribe(institutionId => {
      this.teacherControl.setValue(null);
      if (institutionId) {
        this.loadTeachers(institutionId);
      } else {
        this.loadTeachers(); // Load all if no institution selected
      }
      this.applyFilters();
    });

    this.levelControl.valueChanges.subscribe(() => this.applyFilters());
    this.teacherControl.valueChanges.subscribe(() => this.applyFilters());

    // Dropdown search filters
    this.instFilterCtrl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterInstitutions();
      });

    this.teacherFilterCtrl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterTeachers();
      });
  }

  private filterInstitutions() {
    let search = this.instFilterCtrl.value || '';
    if (!search) {
      this.filteredInstitutions = this.institutions;
      return;
    } else {
      search = search.toLowerCase();
    }
    this.filteredInstitutions = this.institutions.filter(inst =>
      inst.name.toLowerCase().indexOf(search) > -1
    );
  }

  private filterTeachers() {
    let search = this.teacherFilterCtrl.value || '';
    if (!search) {
      this.filteredTeachers = this.teachers;
      return;
    } else {
      search = search.toLowerCase();
    }
    this.filteredTeachers = this.teachers.filter(teacher =>
      (teacher.firstName + ' ' + teacher.lastName).toLowerCase().indexOf(search) > -1
    );
  }

  applyFilters() {
    const searchTerm = this.searchControl.value || '';
    const institutionId = this.institutionControl.value || undefined;
    const currentLevel = this.levelControl.value || undefined;
    const teacherId = this.teacherControl.value || undefined;

    this.loadStudents(searchTerm, institutionId, currentLevel, undefined, teacherId);
  }

  loadStudents(searchTerm?: string, institutionId?: string, currentLevel?: number, isActive?: boolean, teacherId?: string) {
    this.loading.set(true);
    this.usersService.getUsers(searchTerm, 'Student', isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (users) => {
          const students = users
            .filter(student => !institutionId || student.institutionId === institutionId)
            .map(student => this.toStudent(student));
          this.dataSource.data = students;
        },
        error: (error) => {
          this.toaster.error('Öğrenciler yüklenirken bir hata oluştu');
        }
      });
  }

  private toStudent(user: import('../../../core/models/user.model').UserDto): Student {
    return {
      id: user.id,
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      institutionId: user.institutionId,
      institutionName: user.institutionName,
      currentLevel: (user as any).currentLevel ?? 0,
      targetWPM: (user as any).targetWPM ?? 0,
      targetComprehension: (user as any).targetComprehension ?? 0,
      dailyGoalMinutes: (user as any).dailyGoalMinutes ?? 0,
      learningStyle: user.learningStyle ?? 'visual',
      lastLoginAt: user.lastLoginAt,
      isActive: user.isActive,
      createdAt: user.createdAt ?? new Date(0)
    };
  }

  openDialog(student?: Student) {
    const dialogRef = this.dialog.open(StudentDialogComponent, {
      width: '600px',
      data: student,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  async toggleStudentStatus(student: Student) {
    const action = student.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.confirm(`${student.firstName} ${student.lastName} ${action} mı?`);

    if (confirmed) {
      const updateRequest = {
        id: student.id,
        firstName: student.firstName,
        lastName: student.lastName,
        email: student.email,
        institutionId: student.institutionId,
        currentLevel: student.currentLevel,
        targetWPM: student.targetWPM,
        targetComprehension: student.targetComprehension,
        dailyGoalMinutes: student.dailyGoalMinutes,
        learningStyle: student.learningStyle,
        isActive: !student.isActive,
        teacherId: student.teacherId
      };

      this.studentsService.updateStudent(student.id, updateRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess(
              student.isActive ? 'Öğrenci devre dışı bırakıldı' : 'Öğrenci aktif edildi'
            );
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('İşlem başarısız oldu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  async deleteStudent(student: Student) {
    const confirmed = await this.confirm(
      `"${student.firstName} ${student.lastName}" öğrencisini silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`
    );

    if (confirmed) {
      this.studentsService.deleteStudent(student.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Öğrenci silindi');
            this.applyFilters();
          },
          error: (error) => {
            this.toaster.error('Öğrenci silinirken bir hata oluştu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  viewReport(student: Student) {
    this.router.navigate(['/admin/reports/students', student.id]);
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
  }
}
