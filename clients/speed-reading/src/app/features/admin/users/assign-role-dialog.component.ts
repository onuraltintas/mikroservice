import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { takeUntil, finalize } from 'rxjs/operators';
import { UsersService } from '../../../core/services/users.service';
import { UserDto } from '../../../core/models/user.model';
import { BaseComponent } from '../../../core/components/base.component';

interface RoleInfo {
  name: string;
  label: string;
  description: string;
  color: string;
  icon: string;
}

@Component({
  selector: 'app-assign-role-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatListModule,
    MatDividerModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './assign-role-dialog.component.html',
  styleUrls: ['./assign-role-dialog.component.scss']
})
export class AssignRoleDialogComponent extends BaseComponent implements OnInit {
  private usersService = inject(UsersService);
  // toaster inherited from BaseComponent

  processing = false;
  hasChanges = false;

  availableRoles: RoleInfo[] = [];

  private readonly ROLE_METADATA: { [key: string]: RoleInfo } = {
    'Admin': {
      name: 'Admin',
      label: 'Admin',
      description: 'Tam yetki - sistem yönetimi, kullanıcı ve içerik yönetimi',
      color: '#f44336',
      icon: 'shield'
    },
    'Teacher': {
      name: 'Teacher',
      label: 'Öğretmen',
      description: 'Öğrenci yönetimi, görev atama ve raporlama',
      color: '#2196f3',
      icon: 'school'
    },
    'Student': {
      name: 'Student',
      label: 'Öğrenci',
      description: 'Egzersiz yapma, ilerleme takibi',
      color: '#4caf50',
      icon: 'person'
    },
    'Editor': {
      name: 'Editor',
      label: 'Editör',
      description: 'İçerik oluşturma ve düzenleme',
      color: '#ff9800',
      icon: 'edit'
    },
    'InstitutionAdmin': {
      name: 'InstitutionAdmin',
      label: 'Kurum Yöneticisi',
      description: 'Kurum bazlı kullanıcı ve detaylı rapor yönetimi',
      color: '#9c27b0',
      icon: 'business_center'
    }
  };

  constructor(
    public dialogRef: MatDialogRef<AssignRoleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public user: UserDto
  ) {
    super();
  }

  ngOnInit() {
    // Load data
    this.loadUserRoles();
    this.loadAvailableRoles();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadAvailableRoles() {
    this.usersService.getAvailableRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.availableRoles = roles.map(role => {
            return this.ROLE_METADATA[role.name] || {
              name: role.name,
              label: role.name,
              description: '',
              color: '#607d8b',
              icon: 'verified_user'
            };
          });
        },
        error: (error) => {
          this.toaster.error('Rol listesi yüklenirken bir hata oluştu');
        }
      });
  }

  loadUserRoles() {
    this.usersService.getUserById(this.user.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (userDetail) => {
          this.user.roles = userDetail.roles;
        },
        error: (error) => {
          this.toaster.error('Kullanıcı rolleri yüklenirken bir hata oluştu');
        }
      });
  }

  hasRole(roleName: string): boolean {
    return this.user.roles.includes(roleName);
  }

  toggleRole(roleName: string) {
    if (this.hasRole(roleName)) {
      this.removeRole(roleName);
    } else {
      this.addRole(roleName);
    }
  }

  addRole(roleName: string) {
    this.processing = true;
    this.usersService.assignRole(this.user.id, { roleName })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.processing = false)
      )
      .subscribe({
        next: () => {
          this.user.roles.push(roleName);
          this.handleSuccess(`${this.getRoleInfo(roleName).label} rolü atandı`);
          this.hasChanges = true;
        },
        error: (error) => {
          this.toaster.error('Rol atanırken bir hata oluştu. Lütfen tekrar deneyin.');
        }
      });
  }

  async removeRole(roleName: string) {
    if (this.user.roles.length === 1) {
      this.toaster.warning('Kullanıcının en az bir rolü olmalıdır');
      return;
    }

    const confirmed = await this.confirm(`${this.getRoleInfo(roleName).label} rolünü kaldırmak istediğinizden emin misiniz?`);
    if (!confirmed) {
      return;
    }

    this.processing = true;
    this.usersService.removeRole(this.user.id, roleName)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.processing = false)
      )
      .subscribe({
        next: () => {
          this.user.roles = this.user.roles.filter(r => r !== roleName);
          this.handleSuccess(`${this.getRoleInfo(roleName).label} rolü kaldırıldı`);
          this.hasChanges = true;
        },
        error: (error) => {
          this.toaster.error('Rol kaldırılırken bir hata oluştu. Lütfen tekrar deneyin.');
        }
      });
  }

  getRoleInfo(roleName: string): RoleInfo {
    // 1. Check metadata constant first (fastest and available immediately)
    if (this.ROLE_METADATA[roleName]) {
      return this.ROLE_METADATA[roleName];
    }

    // 2. Check loaded list
    return this.availableRoles.find(r => r.name === roleName) || {
      name: roleName,
      label: roleName,
      description: '',
      color: '#9e9e9e',
      icon: 'person'
    };
  }

  getInitials(): string {
    return `${this.user.firstName.charAt(0)}${this.user.lastName.charAt(0)}`.toUpperCase();
  }

  onClose() {
    if (this.processing) {
      return;
    }
    this.dialogRef.close(this.hasChanges);
  }
}
