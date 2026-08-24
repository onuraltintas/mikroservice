import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface Feature {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-features-section',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatCardModule],
  templateUrl: './features-section.html',
  styleUrl: './features-section.scss'
})
export class FeaturesSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  // Default features (fallback)
  features: Feature[] = [
    {
      icon: 'speed',
      title: 'Hızlı Okuma Teknikleri',
      description: 'Göz hareketleri, periferal görüş ve chunking teknikleriyle okuma hızınızı artırın'
    },
    {
      icon: 'psychology',
      title: 'Kavrama Geliştirme',
      description: 'Aktif okuma stratejileri ve hafıza teknikleriyle anlama düzeyinizi %80\'in üzerine çıkarın'
    },
    {
      icon: 'trending_up',
      title: 'Kişiselleştirilmiş Program',
      description: 'Yapay zeka destekli sistem, seviyenize özel egzersizler ve hedefler belirler'
    },
    {
      icon: 'analytics',
      title: 'Detaylı Raporlama',
      description: 'İlerlemenizi takip edin, güçlü ve zayıf yönlerinizi analiz edin'
    },
    {
      icon: 'school',
      title: 'Uzman Eğitmenler',
      description: 'Sertifikalı hızlı okuma eğitmenlerinden canlı destek alın'
    },
    {
      icon: 'emoji_events',
      title: 'Gamification',
      description: 'Rozetler, seviye sistemi ve liderlik tablosu ile motivasyonunuzu yüksek tutun'
    }
  ];

  ngOnInit() {
    this.loadContent();
  }

  private loadContent() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['features_list']) {
          try {
            const parsedFeatures = JSON.parse(content.blocks['features_list']);
            if (Array.isArray(parsedFeatures) && parsedFeatures.length > 0) {
              this.features = parsedFeatures;
            }
          } catch (e) {
            console.warn('Failed to parse features_list, using defaults');
          }
        }
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
      }
    });
  }
}
