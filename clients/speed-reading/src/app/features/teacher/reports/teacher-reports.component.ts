import { Component, DestroyRef, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TeachersService } from '../../../core/services/teachers.service';
import { AuthService } from '../../../core/services/auth.service';
import { Teacher } from '../../../core/models/teacher.model';

@Component({
  selector: 'app-teacher-reports',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatTabsModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    FormsModule
  ],
  templateUrl: './teacher-reports.component.html',
  styleUrls: ['./teacher-reports.component.scss']
})
export class TeacherReportsComponent implements OnInit {
  private teachersService = inject(TeachersService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private teacherSearchChanges = new Subject<string>();
  private teacherListModeActive = false;
  private teacherListRequestId = 0;

  teachers = signal<Teacher[]>([]);
  selectedTeacherId = signal<string | null>(null);
  selectedTeacher = signal<Teacher | null>(null);
  teacherSearchTerm = signal('');
  teacherTotalCount = signal(0);
  teacherPageIndex = signal(0);
  teacherPageSize = signal(25);
  teacherLoading = signal(false);
  teacherLoadError = signal<string | null>(null);
  showDropdown = signal(false);
  reportsReady = computed(() => !this.showDropdown() || !!this.selectedTeacherId());

  ngOnInit() {
    this.teacherSearchChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(searchTerm => {
      this.teacherSearchTerm.set(searchTerm.trim());
      this.teacherPageIndex.set(0);
      this.loadTeachers();
    });

    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const mode = params['mode'];
      const isInstAdmin = this.authService.hasRole('InstitutionAdmin')
        || this.authService.hasRole('InstitutionOwner');

      if (isInstAdmin && mode === 'teacher') {
        this.showDropdown.set(true);
        if (!this.teacherListModeActive) {
          this.teacherSearchTerm.set('');
          this.teacherPageIndex.set(0);
          this.loadTeachers();
        }
        this.teacherListModeActive = true;
        this.setSelectedTeacher(params['teacherId'] || null);
      } else {
        this.showDropdown.set(false);
        this.teacherListModeActive = false;
        this.setSelectedTeacher(null);
      }
    });
  }

  loadTeachers() {
    const requestId = ++this.teacherListRequestId;
    this.teacherLoading.set(true);
    this.teacherLoadError.set(null);
    this.teachersService.getTeachersPage(
      this.teacherPageIndex() + 1,
      this.teacherPageSize(),
      this.teacherSearchTerm() || undefined,
      undefined,
      true
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: page => {
        if (requestId !== this.teacherListRequestId) return;
        this.teachers.set(page.items);
        this.teacherTotalCount.set(page.totalCount);
        this.teacherLoading.set(false);
        const selected = page.items.find(item => item.id === this.selectedTeacherId());
        if (selected) this.selectedTeacher.set(selected);
      },
      error: () => {
        if (requestId !== this.teacherListRequestId) return;
        this.teacherLoading.set(false);
        this.teacherLoadError.set('Öğretmen listesi yüklenemedi. Lütfen yeniden deneyin.');
      }
    });
  }

  onTeacherSelect(teacherId: string) {
    this.setSelectedTeacher(teacherId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { teacherId: teacherId },
      queryParamsHandling: 'merge'
    });
  }

  onTeacherSearchChange(searchTerm: string): void {
    this.teacherSearchChanges.next(searchTerm);
  }

  onTeacherPageChange(event: PageEvent): void {
    this.teacherPageIndex.set(event.pageIndex);
    this.teacherPageSize.set(event.pageSize);
    this.loadTeachers();
  }

  selectedTeacherIsOnCurrentPage(): boolean {
    return this.teachers().some(teacher => teacher.id === this.selectedTeacherId());
  }

  private setSelectedTeacher(teacherId: string | null): void {
    this.selectedTeacherId.set(teacherId);
    if (!teacherId) {
      this.selectedTeacher.set(null);
      return;
    }

    const onPage = this.teachers().find(item => item.id === teacherId);
    if (onPage) {
      this.selectedTeacher.set(onPage);
      return;
    }

    if (this.selectedTeacher()?.id === teacherId) return;
    this.selectedTeacher.set(null);
    this.teachersService.getTeacherById(teacherId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: teacher => {
        if (this.selectedTeacherId() === teacherId) this.selectedTeacher.set(teacher);
      },
      error: () => {
        if (this.selectedTeacherId() === teacherId) this.selectedTeacher.set(null);
      }
    });
  }
}
