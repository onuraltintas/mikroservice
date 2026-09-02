import { Component, HostListener, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { ViewportScroller } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LanguageService } from '../../../core/services/language.service';
import { NavigationItemDto, PublicCmsService } from '../../../core/services/public-cms.service';

interface PublicNavLink {
  href: string;
  label: string;
  fragment?: string;
  openInNewTab: boolean;
  external: boolean;
}

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
  private cmsService = inject(PublicCmsService);
  currentLanguage = this.languageService.currentLanguage;

  navLinks: PublicNavLink[] = [
    { href: '/', label: 'Özellikler', fragment: 'ozellikler', openInNewTab: false, external: false },
    { href: '/', label: 'Fiyatlandırma', fragment: 'fiyatlandirma', openInNewTab: false, external: false },
    { href: '/blog', label: 'Blog', openInNewTab: false, external: false },
    { href: '/hakkimizda', label: 'Hakkımızda', openInNewTab: false, external: false },
    { href: '/iletisim', label: 'İletişim', openInNewTab: false, external: false }
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
    this.cmsService.getNavigation('Main').subscribe({
      next: items => {
        if (items.length > 0) {
          this.navLinks = items.map(item => this.toNavLink(item));
        }
      },
      error: () => {
        // Keep the safe built-in navigation when CMS is unavailable.
      }
    });
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

  onNavLinkClick(event: Event, link: PublicNavLink) {
    const fragment = link.fragment;
    if (fragment) {
      event.preventDefault();

      // Check if we're already on the home page
      if (this.router.url === '/' || this.router.url.startsWith('/#')) {
        // Already on home, just scroll
        this.scrollToFragment(fragment);
      } else {
        // Navigate to home first, then scroll
        this.router.navigate(['/'], { fragment }).then(() => {
          setTimeout(() => this.scrollToFragment(fragment), 100);
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

  private toNavLink(item: NavigationItemDto): PublicNavLink {
    const href = item.url.trim() || '/';
    return {
      href,
      label: item.label,
      fragment: item.fragment?.trim() || undefined,
      openInNewTab: item.openInNewTab,
      external: /^https?:\/\//i.test(href)
    };
  }
}
