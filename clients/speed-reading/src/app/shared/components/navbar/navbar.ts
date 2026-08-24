import { Component, HostListener, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { ViewportScroller } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule, MatButtonModule, MatIconModule],
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss'
})
export class NavbarComponent implements OnInit {
  @Input() forceOpaque = false;
  isScrolled = false;
  mobileMenuOpen = false;

  private languageService = inject(LanguageService);
  currentLanguage = this.languageService.currentLanguage;

  navLinks = [
    { href: '/#ozellikler', label: 'Özellikler', fragment: 'ozellikler' as string | undefined },
    { href: '/#fiyatlandirma', label: 'Fiyatlandırma', fragment: 'fiyatlandirma' as string | undefined },
    { href: '/blog', label: 'Blog', fragment: undefined },
    { href: '/hakkimizda', label: 'Hakkımızda', fragment: undefined },
    { href: '/iletisim', label: 'İletişim', fragment: undefined }
  ];

  constructor(
    private router: Router,
    private viewportScroller: ViewportScroller
  ) { }

  switchLanguage(lang: string): void {
    if (this.languageService.currentLanguage() !== lang) {
      this.languageService.setLanguage(lang);
      window.location.reload();
    }
  }

  ngOnInit() {
    this.checkScroll();
  }

  @HostListener('window:scroll', [])
  onWindowScroll() {
    this.checkScroll();
  }

  checkScroll() {
    this.isScrolled = this.forceOpaque || window.pageYOffset > 50;
  }

  toggleMobileMenu() {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  closeMobileMenu() {
    this.mobileMenuOpen = false;
  }

  onNavLinkClick(event: Event, link: any) {
    if (link.fragment) {
      event.preventDefault();

      // Check if we're already on the home page
      if (this.router.url === '/' || this.router.url.startsWith('/#')) {
        // Already on home, just scroll
        this.scrollToFragment(link.fragment);
      } else {
        // Navigate to home first, then scroll
        this.router.navigate(['/'], { fragment: link.fragment }).then(() => {
          setTimeout(() => this.scrollToFragment(link.fragment), 100);
        });
      }

      this.closeMobileMenu();
    }
  }

  onLogoClick(event: Event) {
    event.preventDefault();

    // Check if we're already on the home page
    if (this.router.url === '/' || this.router.url.startsWith('/#')) {
      // Already on home, scroll to top
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      // Navigate to home
      this.router.navigate(['/']);
    }

    this.closeMobileMenu();
  }

  private scrollToFragment(fragment: string) {
    const element = document.getElementById(fragment);
    if (element) {
      const yOffset = -80; // Navbar height offset
      const y = element.getBoundingClientRect().top + window.pageYOffset + yOffset;
      window.scrollTo({ top: y, behavior: 'smooth' });
    }
  }
}
