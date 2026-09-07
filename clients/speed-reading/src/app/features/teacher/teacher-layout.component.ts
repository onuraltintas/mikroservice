import { Component, OnInit, inject } from '@angular/core';
import { BaseLayoutComponent } from '../../shared/layouts/base-layout/base-layout.component';
import { NavigationService, MenuItem } from '../../core/services/navigation.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-teacher-layout',
  standalone: true,
  imports: [BaseLayoutComponent],
  templateUrl: './teacher-layout.component.html'
})
export class TeacherLayoutComponent implements OnInit {
  private navigationService = inject(NavigationService);
  private authService = inject(AuthService);

  menuItems: MenuItem[] = [];
  title = 'Öğretmen Paneli';

  ngOnInit(): void {
    const isInstitutionAdmin = this.authService.hasRole('InstitutionAdmin');

    if (isInstitutionAdmin) {
      this.menuItems = this.navigationService.getInstitutionAdminMenuItems();
      this.title = 'Kurum Paneli';
    } else {
      this.menuItems = this.navigationService.getTeacherMenuItems();
    }
  }
}
