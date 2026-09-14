import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';

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
export class FeaturesSectionComponent {
  // Default features (fallback)
  features: Feature[] = [
    {
      icon: 'speed',
      title: 'Hızlı Okuma Teknikleri',
      description: 'Odak, metin takibi ve kelime gruplama çalışmalarıyla akıcılığınızı geliştirin'
    },
    {
      icon: 'psychology',
      title: 'Kavrama Geliştirme',
      description: 'Aktif okuma ve soru çalışmalarıyla anlama becerinizi düzenli olarak ölçün'
    },
    {
      icon: 'trending_up',
      title: 'Kişiselleştirilmiş Program',
      description: 'Ölçülen hız ve anlama verilerine göre uygun egzersizler ve hedefler önerilir'
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

}
