import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { TeachersService } from '../../../core/services/teachers.service';
import { AuthService } from '../../../core/services/auth.service';

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

  teachers = signal<any[]>([]);
  selectedTeacherId = signal<string | null>(null);
  showDropdown = signal(false);

  ngOnInit() {
    // Check role and query params
    this.route.queryParams.subscribe(params => {
      const mode = params['mode'];
      const isInstAdmin = this.authService.hasRole('InstitutionAdmin');
      const isAdmin = this.authService.hasAdminAccess();

      // Show dropdown only if user is Admin AND mode is 'teacher'
      if ((isInstAdmin || isAdmin) && mode === 'teacher') {
        this.showDropdown.set(true);
        this.loadTeachers(); // Ensure teachers are loaded

        // If teacher mode but no teacher selected, maybe select first? 
        // Or keep it null (which backend handles as All). 
        // But user wants "Teacher Based", so we should ideally force a selection or show "Select Teacher".
        if (params['teacherId']) {
          this.selectedTeacherId.set(params['teacherId']);
        }
      } else {
        this.showDropdown.set(false);
        // If institution mode, clear teacher selection so reports show full institution data
        if (mode === 'institution') {
          this.selectedTeacherId.set(null);
        }
      }
    });
  }

  // Removed old checkRole/loadTeachers logic since it's merged above
  loadTeachers() {
    if (this.teachers().length > 0) return; // Don't reload if already loaded

    this.teachersService.getTeachers().subscribe({
      next: (data) => this.teachers.set(data),
      error: (err) => console.error('Error loading teachers for selector', err)
    });
  }

  onTeacherSelect(teacherId: string) {
    this.selectedTeacherId.set(teacherId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { teacherId: teacherId },
      queryParamsHandling: 'merge'
    });
  }
}
