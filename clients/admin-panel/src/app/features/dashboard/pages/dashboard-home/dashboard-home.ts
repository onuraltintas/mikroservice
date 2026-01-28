import { Component, inject, signal, PLATFORM_ID, OnInit } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IdentityService } from '../../../../core/services/identity.service';

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
  private platformId = inject(PLATFORM_ID);

  loading = signal(true);
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
      this.loadStats();
    }
  }

  async loadStats() {
    this.loading.set(true);
    try {
      const [usersResult, roles, permissions] = await Promise.all([
        this.identityService.getAllUsers(1, 1000, '').toPromise(),
        this.identityService.getAllRoles().toPromise(),
        this.identityService.getPermissions().toPromise()
      ]);

      const users = usersResult?.items || [];
      const activeUsers = users.filter(u => u.isActive).length;
      const rolesData = roles || [];
      const permsData = permissions || [];

      this.stats.set({
        totalUsers: users.length,
        activeUsers: activeUsers,
        inactiveUsers: users.length - activeUsers,
        totalRoles: rolesData.filter(r => !r.isDeleted).length,
        systemRoles: rolesData.filter(r => r.isSystemRole && !r.isDeleted).length,
        customRoles: rolesData.filter(r => !r.isSystemRole && !r.isDeleted).length,
        totalPermissions: permsData.filter(p => !p.isDeleted).length,
        systemPermissions: permsData.filter(p => p.isSystem && !p.isDeleted).length,
        customPermissions: permsData.filter(p => !p.isSystem && !p.isDeleted).length
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
