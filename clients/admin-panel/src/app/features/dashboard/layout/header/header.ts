import { Component, signal, OnInit, inject, PLATFORM_ID, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';
import { DarkModeService } from '../../../../core/services/dark-mode.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent {
  private authService = inject(AuthService);
  private toaster = inject(ToasterService);
  layoutService = inject(LayoutService);
  darkModeService = inject(DarkModeService);

  showUserMenu = signal(false);
  user = this.authService.userProfile;

  toggleTheme() {
    this.darkModeService.toggle();
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
}
