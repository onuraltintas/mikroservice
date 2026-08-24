import { Component, OnInit, inject, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';
import { UsersService } from '../../../core/services/users.service';
import { UserDto, UserDetailDto, RoleDto } from '../../../core/models/user.model';
import { UserDialogComponent } from './user-dialog.component';
import { AssignRoleDialogComponent } from './assign-role-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';

@Component({
  selector: 'app-users-list',
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
    MatChipsModule,
    MatBadgeModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './users-list.component.html',
  styleUrls: ['./users-list.component.scss']
})
export class UsersListComponent extends BaseComponent implements OnInit, AfterViewInit {
  private usersService = inject(UsersService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new MatTableDataSource<UserDto>([]);
  displayedColumns = ['index', 'avatar', 'fullName', 'email', 'roles', 'institution', 'status', 'lastLogin', 'actions'];
  // loading inherited from BaseComponent

  searchControl = new FormControl('');
  roleControl = new FormControl('');
  statusControl = new FormControl<boolean | null>(null);

  roles: RoleDto[] = [];

  ngOnInit() {
    this.loadRoles();
    this.setupFilters();
    this.loadUsers();
  }

  loadRoles() {
    this.usersService.getAvailableRoles().subscribe({
      next: (roles) => {
        this.roles = roles;
      },
      error: (err) => console.error('Error loading roles', err)
    });
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Custom filter predicate
    this.dataSource.filterPredicate = (data: UserDto, filter: string) => {
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
      .subscribe(() => this.applyFilters());

    this.roleControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());
  }

  applyFilters() {
    const searchTerm = this.searchControl.value || '';
    const role = this.roleControl.value || '';
    const status = this.statusControl.value;

    this.loadUsers(searchTerm, role, status ?? undefined);
  }

  loadUsers(searchTerm?: string, role?: string, isActive?: boolean) {
    this.loading.set(true);
    this.usersService.getUsers(searchTerm, role, isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (users) => {
          this.dataSource.data = users;
        },
        error: (error) => {
          this.toaster.error('Kullanıcılar yüklenirken bir hata oluştu');
        }
      });
  }

  openDialog(user?: UserDto) {
    // If editing, fetch full user details with profile data
    if (user) {
      this.usersService.getUserById(user.id).subscribe({
        next: (userDetail: UserDetailDto) => {
          this.openUserDialog(userDetail);
        },
        error: (error) => {
          console.error('Error loading user details:', error);
          this.toaster.error('Kullanıcı detayları yüklenirken hata oluştu', 3000);
        }
      });
    } else {
      // For new user, no data needed
      this.openUserDialog(null);
    }
  }

  private openUserDialog(user: UserDetailDto | null) {
    const dialogRef = this.dialog.open(UserDialogComponent, {
      width: '950px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: user,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  openRoleDialog(user: UserDto) {
    const dialogRef = this.dialog.open(AssignRoleDialogComponent, {
      width: '500px',
      data: user,
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  viewUser(user: UserDto) {
    this.usersService.getUserById(user.id).subscribe({
      next: (userDetail) => {
        this.toaster.info(`Kullanıcı detayları: ${userDetail.firstName} ${userDetail.lastName}`, 3500);
        // TODO: Implement user detail view
      },
      error: (error) => {
        console.error('Error loading user details:', error);
        this.toaster.error('Kullanıcı detayları yüklenirken hata oluştu', 3000);
      }
    });
  }

  async toggleEmailConfirmation(user: UserDto) {
    const action = user.emailConfirmed ? 'email onayı iptal edilsin' : 'email onaylansın';
    const confirmed = await this.toaster.confirm(`${user.firstName} ${user.lastName} kullanıcısının ${action} mı?`);
    if (confirmed) {
      const operation = user.emailConfirmed
        ? this.usersService.revokeEmailConfirmation(user.id)
        : this.usersService.confirmEmail(user.id);

      operation.subscribe({
        next: () => {
          this.toaster.success(user.emailConfirmed ? 'Email onayı iptal edildi' : 'Email onaylandı', 3000);
          this.applyFilters();
        },
        error: (error) => {
          console.error('Error toggling email confirmation:', error);
          this.toaster.error('İşlem sırasında hata oluştu', 3000);
        }
      });
    }
  }

  async toggleUserStatus(user: UserDto) {
    const action = user.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.toaster.confirm(`${user.firstName} ${user.lastName} kullanıcısı ${action} mı?`);
    if (confirmed) {
      const operation = user.isActive
        ? this.usersService.deactivateUser(user.id)
        : this.usersService.activateUser(user.id);

      operation.subscribe({
        next: () => {
          this.toaster.success(user.isActive ? 'Kullanıcı devre dışı bırakıldı' : 'Kullanıcı aktif edildi', 3000);
          this.applyFilters();
        },
        error: (error) => {
          console.error('Error toggling user status:', error);
          this.toaster.error('İşlem sırasında hata oluştu', 3000);
        }
      });
    }
  }

  async deleteUser(user: UserDto) {
    const confirmed = await this.toaster.confirm(
      `"${user.firstName} ${user.lastName}" kullanıcısını silmek istediğinizden emin misiniz?\n\n` +
      `• Kullanıcı hesabı anonimleştirilecek.\n` +
      `• İlerlemeler ve veriler sistemde 'Silinmiş Kullanıcı' olarak kalacak.\n` +
      `• E-posta adresi boşa çıkacak ve tekrar kullanılabilir olacak.`
    );
    if (confirmed) {
      this.usersService.deleteUser(user.id).subscribe({
        next: () => {
          this.toaster.success('Kullanıcı silindi', 3000);
          this.applyFilters();
        },
        error: (error) => {
          console.error('Error deleting user:', error);
          this.toaster.error('Kullanıcı silinirken hata oluştu', 3000);
        }
      });
    }
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
  }

  getRoleLabel(role: string): string {
    const labels: { [key: string]: string } = {
      'Admin': 'Admin',
      'Teacher': 'Öğretmen',
      'Student': 'Öğrenci',
      'Editor': 'Editör'
    };
    return labels[role] || role;
  }

  getRoleColor(role: string): string {
    const colors: { [key: string]: string } = {
      'Admin': '#f44336',
      'Teacher': '#2196f3',
      'Student': '#4caf50',
      'Editor': '#ff9800'
    };
    return colors[role] || '#9e9e9e';
  }

  getAvatarBackground(user: UserDto): string {
    const colors = [
      'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      'linear-gradient(135deg, #2af598 0%, #009efd 100%)',
      'linear-gradient(135deg, #ff9a9e 0%, #fecfef 99%, #fecfef 100%)',
      'linear-gradient(135deg, #f6d365 0%, #fda085 100%)',
      'linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%)',
      'linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)'
    ];
    const index = (user.firstName.length + user.lastName.length) % colors.length;
    return colors[index];
  }
}
