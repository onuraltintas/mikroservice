import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { filter } from 'rxjs';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  activeRoutes: string[]; // Routes that make this nav item active
}

@Component({
  selector: 'app-bottom-navigation',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule
  ],
  templateUrl: './bottom-navigation.html',
  styleUrl: './bottom-navigation.scss'
})
export class BottomNavigationComponent {
  currentRoute = '';

  navItems: NavItem[] = [
    {
      label: 'Ana Sayfa',
      icon: 'home',
      route: '/student/dashboard',
      activeRoutes: ['/student/dashboard', '/student']
    },
    {
      label: 'Egzersizler',
      icon: 'fitness_center',
      route: '/student/exercises',
      activeRoutes: ['/student/exercises', '/student/exercise']
    },
    {
      label: 'Okuma',
      icon: 'menu_book',
      route: '/student/reading',
      activeRoutes: ['/student/reading', '/student/reading-text']
    },
    {
      label: 'İlerleme',
      icon: 'trending_up',
      route: '/student/progress',
      activeRoutes: ['/student/progress', '/student/analytics']
    },
    {
      label: 'Profil',
      icon: 'person',
      route: '/student/profile',
      activeRoutes: ['/student/profile', '/student/settings']
    }
  ];

  constructor(private router: Router) {
    // Track current route
    this.currentRoute = this.router.url;

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentRoute = event.urlAfterRedirects || event.url;
      });
  }

  isActive(item: NavItem): boolean {
    return item.activeRoutes.some(route => this.currentRoute.startsWith(route));
  }

  navigate(route: string): void {
    this.router.navigate([route]);
  }
}
