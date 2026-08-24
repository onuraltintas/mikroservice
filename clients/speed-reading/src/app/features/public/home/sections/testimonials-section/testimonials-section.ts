import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface Testimonial {
  name: string;
  role: string;
  rating: number;
  text: string;
  avatar: string;
}

@Component({
  selector: 'app-testimonials-section',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './testimonials-section.html',
  styleUrl: './testimonials-section.scss'
})
export class TestimonialsSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  // Default testimonials (fallback)
  testimonials: Testimonial[] = [
    {
      name: 'Ahmet Yılmaz',
      role: 'Üniversite Öğrencisi',
      rating: 5,
      text: 'Okuma hızım dakikada 200 kelimeden 600 kelimeye çıktı! Sınavlara hazırlanırken çok büyük avantaj sağladı.',
      avatar: 'AY'
    },
    {
      name: 'Zeynep Kaya',
      role: 'Yazılım Geliştirici',
      rating: 5,
      text: 'Teknik dokümantasyonları çok daha hızlı okuyabiliyorum. İş verimliliğim %40 arttı.',
      avatar: 'ZK'
    },
    {
      name: 'Mehmet Demir',
      role: 'Lise Öğrencisi',
      rating: 4,
      text: 'Ders çalışma sürem yarıya indi ama anlama seviyem aynı kaldı. Harika bir platform!',
      avatar: 'MD'
    },
    {
      name: 'Ayşe Şahin',
      role: 'Öğretmen',
      rating: 5,
      text: 'Öğrencilerime de tavsiye ediyorum. Gamification özellikleri motivasyonu çok artırıyor.',
      avatar: 'AŞ'
    }
  ];

  ngOnInit() {
    this.loadContent();
  }

  private loadContent() {
    // Fetch from 'AboutPage' to share the same testimonials
    this.cmsService.getLandingContent('AboutPage').subscribe({
      next: (content) => {
        if (content.blocks['about_testimonials']) {
          try {
            const parsedTestimonials = JSON.parse(content.blocks['about_testimonials']);
            if (Array.isArray(parsedTestimonials) && parsedTestimonials.length > 0) {
              this.testimonials = parsedTestimonials.map((t: any) => ({
                name: t.name,
                role: t.role,
                rating: t.rating,
                text: t.text,
                avatar: t.avatar || this.getInitials(t.name)
              }));
            }
          } catch (e) {
            console.warn('Failed to parse about_testimonials, using defaults');
          }
        }
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
      }
    });
  }

  private getInitials(name: string): string {
    return name
      .split(' ')
      .map(n => n[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  getStars(rating: number): number[] {
    return Array(5).fill(0).map((_, i) => i < rating ? 1 : 0);
  }
}
