import { Component, OnInit, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface FaqItem {
  question: string;
  answer: string;
  category: string;
}

@Component({
  selector: 'app-faq-section',
  standalone: true,
  imports: [CommonModule, RouterLink, MatExpansionModule, MatIconModule],
  templateUrl: './faq-section.html',
  styleUrl: './faq-section.scss',
  encapsulation: ViewEncapsulation.None
})
export class FaqSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  // Default FAQs (fallback)
  faqs: FaqItem[] = [
    {
      question: 'Hızlı okuma öğrenmek ne kadar sürer?',
      answer: 'Ortalama 4-6 hafta düzenli çalışma ile belirgin gelişme görürsünüz. Günde 15-20 dakika pratik yapmanız yeterlidir.',
      category: 'Genel'
    },
    {
      question: 'Anlama seviyem düşer mi?',
      answer: 'Hayır, aksine anlama seviyeniz artar! Programımız hızlı okuma ile birlikte kavrama tekniklerini de öğretir.',
      category: 'Genel'
    },
    {
      question: 'Hangi yaş grupları için uygun?',
      answer: '10 yaş ve üzeri herkes için uygundur. Özellikle öğrenciler ve profesyoneller için idealdir.',
      category: 'Genel'
    },
    {
      question: 'Mobil cihazlardan kullanabilir miyim?',
      answer: 'Evet! Platformumuz responsive tasarıma sahiptir ve tüm cihazlarda sorunsuz çalışır.',
      category: 'Teknik'
    },
    {
      question: 'Para iade garantiniz var mı?',
      answer: 'Evet, ilk 7 gün içinde %100 para iadesi garantisi sunuyoruz. Hiçbir soru sormadan iade yapabilirsiniz.',
      category: 'Fiyatlandırma'
    },
    {
      question: 'Sertifika alabilir miyim?',
      answer: 'Pro ve Kurumsal planlarda dijital sertifika alırsınız. Sertifika LinkedIn profilinizde paylaşılabilir.',
      category: 'Sertifika'
    },
    {
      question: 'Öğretmen desteği var mı?',
      answer: 'Pro planda öncelikli email desteği, Kurumsal planda ise özel eğitmen desteği bulunmaktadır.',
      category: 'Destek'
    }
  ];

  ngOnInit() {
    this.loadContent();
  }

  private loadContent() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['faq_list']) {
          try {
            const parsedFaqs = JSON.parse(content.blocks['faq_list']);
            if (Array.isArray(parsedFaqs) && parsedFaqs.length > 0) {
              this.faqs = parsedFaqs;
            }
          } catch (e) {
            console.warn('Failed to parse faq_list, using defaults');
          }
        }
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
      }
    });
  }
}
