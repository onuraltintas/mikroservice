import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterModule } from '@angular/router';

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
export class HeroSectionComponent {
  private router = inject(Router);

  // Default content (fallback)
  title = 'Hız ve Anlamayı Birlikte Geliştirin';
  subtitle = 'Hızınızı ve anlama becerinizi birlikte ölçerek size uygun bir çalışma akışı oluşturun.';
  stats: Stat[] = [
    { label: 'Başlangıç', value: 'Ölçüm' },
    { label: 'Çalışma', value: 'Kişisel plan' },
    { label: 'İlerleme', value: 'Hız + anlama' }
  ];

  startFreeTrial() {
    this.router.navigate(['/auth/register']);
  }

  scrollToFeatures() {
    const element = document.getElementById('ozellikler');
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

}
