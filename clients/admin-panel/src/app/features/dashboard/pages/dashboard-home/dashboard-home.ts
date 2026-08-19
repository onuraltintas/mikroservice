import { Component, inject, signal, PLATFORM_ID, OnInit } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IdentityService } from '../../../../core/services/identity.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { firstValueFrom } from 'rxjs';

interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  totalRoles: number;
  systemRoles: number;
  customRoles: number;
  totalPermissions: number;
  systemPermissions: number;
  customPermissions: number;
}

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard-home.html',
  styleUrl: './dashboard-home.scss',
})
export class DashboardHomeComponent implements OnInit {
  private identityService = inject(IdentityService);
  private authService = inject(AuthService);
  private platformId = inject(PLATFORM_ID);

  loading = signal(true);
  can = (permission: string) => this.authService.hasPermission(permission);
  readonly permissions = ADMIN_PERMISSIONS;
  stats = signal<DashboardStats>({
    totalUsers: 0,
    activeUsers: 0,
    inactiveUsers: 0,
    totalRoles: 0,
    systemRoles: 0,
    customRoles: 0,
    totalPermissions: 0,
    systemPermissions: 0,
    customPermissions: 0
  });

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      const user = this.authService.userProfile();
      // Only load stats if user has Admin privileges
      if (user?.permissions?.includes(ADMIN_PERMISSIONS.usersView)) {
        this.loadStats();
      } else {
        this.loading.set(false);
      }
    }
  }

  async loadStats() {
    this.loading.set(true);
    try {
      const canViewUsers = this.authService.hasPermission(ADMIN_PERMISSIONS.usersView);
      const canViewRoles = this.authService.hasPermission(ADMIN_PERMISSIONS.rolesView);
      const canViewPermissions = this.authService.hasPermission(ADMIN_PERMISSIONS.permissionView);
      const [usersResponse, rolesResponse, permissionsResponse] = await Promise.allSettled([
        canViewUsers ? firstValueFrom(this.identityService.getUserSummary()) : Promise.resolve(null),
        canViewRoles ? firstValueFrom(this.identityService.getAllRoles()) : Promise.resolve(null),
        canViewPermissions ? firstValueFrom(this.identityService.getPermissions()) : Promise.resolve(null)
      ]);

      const usersResult = usersResponse.status === 'fulfilled' ? usersResponse.value : null;
      const roles = rolesResponse.status === 'fulfilled' ? rolesResponse.value : null;
      const permissions = permissionsResponse.status === 'fulfilled' ? permissionsResponse.value : null;

      const totalUsers = usersResult?.totalUsers ?? 0;
      const activeUsers = usersResult?.activeUsers ?? 0;
      const rolesData = roles || [];
      const permsData = permissions || [];

      this.stats.set({
        totalUsers,
        activeUsers: activeUsers,
        inactiveUsers: usersResult?.inactiveUsers ?? 0,
        totalRoles: rolesData.filter((r: any) => !r.isDeleted).length,
        systemRoles: rolesData.filter((r: any) => r.isSystemRole && !r.isDeleted).length,
        customRoles: rolesData.filter((r: any) => !r.isSystemRole && !r.isDeleted).length,
        totalPermissions: permsData.filter((p: any) => !p.isDeleted).length,
        systemPermissions: permsData.filter((p: any) => p.isSystem && !p.isDeleted).length,
        customPermissions: permsData.filter((p: any) => !p.isSystem && !p.isDeleted).length
      });
    } catch (error) {
      console.error('Error loading dashboard stats:', error);
    } finally {
      this.loading.set(false);
    }
  }

  getActivePercentage(): number {
    const s = this.stats();
    if (s.totalUsers === 0) return 0;
    return Math.round((s.activeUsers / s.totalUsers) * 100);
  }
}
