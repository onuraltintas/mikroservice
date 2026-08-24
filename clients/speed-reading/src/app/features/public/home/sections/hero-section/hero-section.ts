import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterModule } from '@angular/router';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface Stat {
  label: string;
  value: string;
}

@Component({
  selector: 'app-hero-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, RouterModule],
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.scss'
})
export class HeroSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);
  private router = inject(Router);

  // Default content (fallback)
  title = 'Hızlı Okuma Öğrenin, Hayatınızı Değiştirin';
  subtitle = 'Bilimsel yöntemlerle okuma hızınızı 3 katına çıkarın. 50,000+ mutlu kullanıcımıza katılın!';
  stats: Stat[] = [
    { label: 'Aktif Kullanıcı', value: '50,000+' },
    { label: 'Okunan Kitap', value: '1M+' },
    { label: 'Başarı Oranı', value: '%95' }
  ];

  ngOnInit() {
    this.loadContent();
  }

  startFreeTrial() {
    this.router.navigate(['/auth/register']);
  }

  scrollToFeatures() {
    const element = document.getElementById('ozellikler');
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  private loadContent() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        // Parse content blocks
        if (content.blocks['hero_title']) {
          this.title = content.blocks['hero_title'];
        }

        if (content.blocks['hero_subtitle']) {
          this.subtitle = content.blocks['hero_subtitle'];
        }

        if (content.blocks['hero_stats']) {
          try {
            const parsedStats = JSON.parse(content.blocks['hero_stats']);
            if (Array.isArray(parsedStats) && parsedStats.length > 0) {
              this.stats = parsedStats;
            }
          } catch (e) {
            console.warn('Failed to parse hero_stats, using default');
          }
        }
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
        // Fallback content already set
      }
    });
  }
}
