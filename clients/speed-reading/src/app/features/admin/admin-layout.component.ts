import { Component, OnInit, inject, signal } from '@angular/core';
import { BaseLayoutComponent } from '../../shared/layouts/base-layout/base-layout.component';
import { NavigationService, MenuItem } from '../../core/services/navigation.service';

import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';
import { Router } from '@angular/router';
import { CmsService } from '../../core/services/cms.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [
    BaseLayoutComponent,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatBadgeModule
  ],
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.scss']
})
export class AdminLayoutComponent implements OnInit {
  private navigationService = inject(NavigationService);
  private cmsService = inject(CmsService);
  private router = inject(Router);

  menuItems: MenuItem[] = [];
  title = 'Yönetici Paneli';

  // Signal for unread count if using Angular 16+ signals, otherwise just a property
  // Using property for now as signals might need deeper refactor or import check
  unreadMessageCount = signal(0);

  // Manual fetch approach for safety
  // unreadMessageCount = signal(0); 

  ngOnInit(): void {
    this.menuItems = this.navigationService.getAdminMenuItems();
    this.checkUnreadMessages();
  }

  checkUnreadMessages() {
    this.cmsService.getUnreadContactMessageCount().subscribe(count => {
      this.unreadMessageCount.set(count);
      // Hack: updating a property if signal not used
      // (this as any).unreadMessageCount = () => count;
    });
  }

  navigateToMessages() {
    this.router.navigate(['/admin/cms/contact-messages']);
  }
}
