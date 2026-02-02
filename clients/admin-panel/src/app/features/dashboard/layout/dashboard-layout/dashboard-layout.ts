import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar';
import { HeaderComponent } from '../header/header';
import { CommonModule } from '@angular/common';
import { LayoutService } from '../../services/layout.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { catchError, of, forkJoin } from 'rxjs';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, HeaderComponent, CommonModule],
  templateUrl: './dashboard-layout.html',
  styleUrls: ['./dashboard-layout.scss']
})
export class DashboardLayoutComponent implements OnInit, OnDestroy {
  layoutService = inject(LayoutService);
  private configService = inject(ConfigurationService);

  inMaintenanceMode = false;
  private intervalId: any;

  ngOnInit() {
    this.checkMaintenanceMode();
    // Poll every 10 seconds
    this.intervalId = setInterval(() => this.checkMaintenanceMode(), 10000);
  }

  ngOnDestroy() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  private checkMaintenanceMode() {
    // Check BOTH System Global Maintenance AND Identity Service Maintenance
    forkJoin({
      system: this.configService.getConfigurationValue('system.maintenancemode').pipe(catchError(() => of('false'))),
      identity: this.configService.getConfigurationValue('maintenance.identity').pipe(catchError(() => of('false')))
    }).subscribe({
      next: (results) => {
        const sysVal = results.system?.replace(/"/g, '').trim().toLowerCase() === 'true';
        const idVal = results.identity?.replace(/"/g, '').trim().toLowerCase() === 'true';

        // Activate banner if EITHER is true
        this.inMaintenanceMode = sysVal || idVal;
      },
      error: () => {
        this.inMaintenanceMode = false;
      }
    });
  }
}
