import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService, LandingContentVm } from '../../../core/services/public-cms.service';

interface FooterLink {
  label: string;
  route: string;
  fragment?: string;
}

interface SocialLink {
  icon: string;
  url: string;
}

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './footer.html',
  styleUrl: './footer.scss'
})
export class FooterComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  currentYear = new Date().getFullYear();

  // Branding
  logoText = 'Hızlı Okuma';
  tagline = 'Okuma hızınızı bilimsel yöntemlerle 3 katına çıkarın';
  copyrightText = 'Hızlı Okuma Platformu. Tüm hakları saklıdır.';

  // Links - Defaults
  productLinks: FooterLink[] = [
    { label: 'Özellikler', route: '/', fragment: 'ozellikler' },
    { label: 'Fiyatlandırma', route: '/', fragment: 'fiyatlandirma' },
    { label: 'Blog', route: '/blog' },
    { label: 'Hakkımızda', route: '/hakkimizda' }
  ];

  supportLinks: FooterLink[] = [
    { label: 'SSS', route: '/sss' },
    { label: 'İletişim', route: '/iletisim' },
    { label: 'Yardım Merkezi', route: '/help' },
    { label: 'Durum', route: '/status' }
  ];

  legalLinks: FooterLink[] = [
    { label: 'Gizlilik Politikası', route: '/legal/privacy' },
    { label: 'Kullanım Şartları', route: '/legal/terms' },
    { label: 'KVKK', route: '/legal/kvkk' },
    { label: 'Çerez Politikası', route: '/legal/cookies' }
  ];

  socialLinks: SocialLink[] = [
    { icon: 'facebook', url: '#' },
    { icon: 'twitter', url: '#' },
    { icon: 'instagram', url: '#' },
    { icon: 'linkedin', url: '#' }
  ];

  ngOnInit() {
    this.loadContent();
  }

  private loadContent() {
    this.cmsService.getLandingContent('Footer').subscribe({
      next: (content: LandingContentVm) => {
        const blocks = content.blocks;

        // Branding
        if (blocks['footer_logo_text']) {
          this.logoText = blocks['footer_logo_text'];
        }
        if (blocks['footer_tagline']) {
          this.tagline = blocks['footer_tagline'];
        }
        if (blocks['footer_copyright']) {
          this.copyrightText = blocks['footer_copyright'];
        }

        // Social Links
        if (blocks['footer_social_links']) {
          try {
            const parsed = JSON.parse(blocks['footer_social_links']);
            if (Array.isArray(parsed) && parsed.length > 0) {
              this.socialLinks = parsed;
            }
          } catch (e) {
            console.warn('Failed to parse footer_social_links');
          }
        }

        // Product Links
        if (blocks['footer_product_links']) {
          try {
            const parsed = JSON.parse(blocks['footer_product_links']);
            if (Array.isArray(parsed) && parsed.length > 0) {
              this.productLinks = parsed;
            }
          } catch (e) {
            console.warn('Failed to parse footer_product_links');
          }
        }

        // Support Links
        if (blocks['footer_support_links']) {
          try {
            const parsed = JSON.parse(blocks['footer_support_links']);
            if (Array.isArray(parsed) && parsed.length > 0) {
              this.supportLinks = parsed;
            }
          } catch (e) {
            console.warn('Failed to parse footer_support_links');
          }
        }

        // Legal Links
        if (blocks['footer_legal_links']) {
          try {
            const parsed = JSON.parse(blocks['footer_legal_links']);
            if (Array.isArray(parsed) && parsed.length > 0) {
              this.legalLinks = parsed;
            }
          } catch (e) {
            console.warn('Failed to parse footer_legal_links');
          }
        }
      },
      error: (err: any) => {
        console.warn('Failed to load footer content, using defaults', err);
      }
    });
  }
}
