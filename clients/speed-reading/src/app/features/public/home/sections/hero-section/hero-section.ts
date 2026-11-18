import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DEFAULT_HOME_PAGE_CONTENT, HomeHeroContent } from '../../home-page-content';

interface Stat {
  label: string;
  value: string;
}

@Component({
  selector: 'app-hero-section',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.scss'
})
export class HeroSectionComponent {
  private router = inject(Router);
  @Input() content: HomeHeroContent = DEFAULT_HOME_PAGE_CONTENT.hero;

  stats: Stat[] = [
    { label: 'Başlangıç', value: 'Seviye ölçümü' },
    { label: 'Çalışma', value: 'Kişisel plan' },
    { label: 'İlerleme', value: 'Hız ve anlama' }
  ];

  startFreeTrial() {
    this.router.navigateByUrl(this.content.primaryActionUrl || '/auth/register');
  }

  scrollToFeatures() {
    const element = document.getElementById('ozellikler');
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

}
