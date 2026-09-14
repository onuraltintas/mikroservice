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
  subtitle = 'Hızınızı ve anlama becerinizi birlikte ölçerek size uygun bir çalışma akışı oluşturun.';
  stats: Stat[] = [
    { label: 'Başlangıç', value: 'Ölçüm' },
    { label: 'Çalışma', value: 'Kişisel plan' },
    { label: 'İlerleme', value: 'Hız + anlama' }
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

        // Outcome claims are code-owned until a controlled study provides
        // verifiable population-level evidence.
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
        // Fallback content already set
      }
    });
  }
}
