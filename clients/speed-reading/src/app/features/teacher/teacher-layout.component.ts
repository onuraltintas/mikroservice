import { Component, OnInit, inject } from '@angular/core';
import { BaseLayoutComponent } from '../../shared/layouts/base-layout/base-layout.component';
import { NavigationService, MenuItem } from '../../core/services/navigation.service';
import { AuthService } from '../../core/services/auth.service';
import { AdminContextService } from '../../core/services/admin-context.service';

@Component({
  selector: 'app-teacher-layout',
  standalone: true,
  imports: [BaseLayoutComponent],
  templateUrl: './teacher-layout.component.html'
})
export class TeacherLayoutComponent implements OnInit {
  private navigationService = inject(NavigationService);
  private authService = inject(AuthService);
  private adminContext = inject(AdminContextService);

  menuItems: MenuItem[] = [];
  title = 'Öğretmen Paneli';

  ngOnInit(): void {
    const user = this.authService.currentUserValue;
    const isInstitutionAdmin = user?.roles?.includes('InstitutionAdmin');
    const isImpersonatingInstitution = this.adminContext.isImpersonatingInstitution();

    if (isInstitutionAdmin || isImpersonatingInstitution) {
      this.menuItems = this.navigationService.getInstitutionAdminMenuItems();
      this.title = isImpersonatingInstitution
        ? `Kurum Paneli (${this.adminContext.institutionName()})`
        : 'Kurum Paneli';
    } else {
      this.menuItems = this.navigationService.getTeacherMenuItems();
      if (this.adminContext.isImpersonatingTeacher()) {
        this.title = `Öğretmen Paneli (${this.adminContext.teacherName()})`;
      }
    }
  }
}
