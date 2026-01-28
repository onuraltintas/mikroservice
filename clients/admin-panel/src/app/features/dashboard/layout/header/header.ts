import { Component, signal, OnInit, inject, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent implements OnInit {
  private authService = inject(AuthService);
  private toaster = inject(ToasterService);
  private platformId = inject(PLATFORM_ID);
  layoutService = inject(LayoutService);

  isDarkMode = signal(false);
  showUserMenu = signal(false);
  user = this.authService.userProfile;

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      // Check local storage or system preference
      if (localStorage.getItem('theme') === 'dark' ||
        (!('theme' in localStorage) && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
        this.setDark(true);
      } else {
        this.setDark(false);
      }
    }
  }

  toggleTheme() {
    this.setDark(!this.isDarkMode());
  }

  toggleUserMenu() {
    this.showUserMenu.update(v => !v);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    // Close menu when clicking outside
    const target = event.target as HTMLElement;
    if (!target.closest('.user-menu-container')) {
      this.showUserMenu.set(false);
    }
  }

  async logout() {
    this.showUserMenu.set(false);
    const confirmed = await this.toaster.confirm(
      'Çıkış Yap',
      'Oturumunuzu sonlandırmak istediğinize emin misiniz?',
      'Evet, Çıkış Yap',
      'İptal'
    );

    if (confirmed) {
      this.authService.logout();
    }
  }

  private setDark(val: boolean) {
    this.isDarkMode.set(val);
    if (isPlatformBrowser(this.platformId)) {
      if (val) {
        document.documentElement.classList.add('dark');
        localStorage.setItem('theme', 'dark');
      } else {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
      }
    }
  }
}
