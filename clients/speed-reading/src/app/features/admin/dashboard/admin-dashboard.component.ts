import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DashboardService, AdminDashboard } from '../../../core/services/dashboard.service';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressBarModule
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.scss']
})
export class AdminDashboardComponent extends BaseComponent implements OnInit, OnDestroy {
  private dashboardService = inject(DashboardService);

  dashboard: AdminDashboard | null = null;
  // loading inherited from BaseComponent

  institutionColumns: string[] = ['name', 'students', 'teachers', 'kdp', 'exercises'];
  contentColumns: string[] = ['title', 'type', 'usage', 'avgTime', 'avgScore'];

  ngOnInit(): void {
    this.loadDashboard();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadDashboard(): void {
    this.loading.set(true);
    this.dashboardService.getAdminDashboard()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (dashboard) => {
          this.dashboard = dashboard;
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading dashboard:', err);
          this.loading.set(false);
        }
      });
  }

  getHealthColor(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }

  formatDuration(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${minutes}dk ${secs}s`;
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('tr-TR');
  }
}
