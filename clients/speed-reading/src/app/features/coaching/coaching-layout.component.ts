import { Component, OnInit, inject } from '@angular/core';
import { BaseLayoutComponent } from '../../shared/layouts/base-layout/base-layout.component';
import { NavigationService, MenuItem } from '../../core/services/navigation.service';

@Component({
  selector: 'app-coaching-layout',
  standalone: true,
  imports: [BaseLayoutComponent],
  template: `<app-base-layout [menuItems]="menuItems" [title]="title" [role]="'teacher'"></app-base-layout>`
})
export class CoachingLayoutComponent implements OnInit {
  private navigationService = inject(NavigationService);

  menuItems: MenuItem[] = [];
  title = 'Koç Paneli';

  ngOnInit(): void {
    this.menuItems = this.navigationService.getCoachingMenuItems();
  }
}
