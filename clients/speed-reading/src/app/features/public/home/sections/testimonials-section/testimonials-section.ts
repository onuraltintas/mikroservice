import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

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
export class TestimonialsSectionComponent {
  // Default testimonials (fallback)
  testimonials: Testimonial[] = [
    {
      name: 'Ölçüm odaklı çalışma',
      role: 'Kişisel hedefler',
      rating: 5,
      text: 'Her çalışma sonrasında hız ve anlama birlikte değerlendirilir; sonraki içerik bu sonuca göre planlanır.',
      avatar: 'ÖÇ'
    },
    {
      name: 'Kademeli ilerleme',
      role: 'Uygun zorluk',
      rating: 5,
      text: 'Yeterli ve tutarlı ölçüm olmadan seviye yükseltilmez; anlama zorlanırsa destek çalışmaları sunulur.',
      avatar: 'Kİ'
    },
    {
      name: 'Şeffaf sonuçlar',
      role: 'Hız ve anlama',
      rating: 5,
      text: 'İlerleme; başlangıç düzeyi, düzenli çalışma ve anlama sonuçlarıyla birlikte değerlendirilir.',
      avatar: 'ŞS'
    },
    {
      name: 'İçerik ve geri bildirim',
      role: 'Öğrenme akışı',
      rating: 5,
      text: 'Egzersizler, metinler ve sorular öğrencinin çalışma geçmişiyle birlikte değerlendirilir.',
      avatar: 'İG'
    }
  ];

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
