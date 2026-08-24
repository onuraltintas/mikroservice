import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

interface QuickLink {
  icon: string;
  label: string;
  route: string;
  color: string;
  queryParams?: any;
}

interface Quote {
  text: string;
  author: string;
}

@Component({
  selector: 'app-quick-access-widget',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './quick-access-widget.component.html',
  styleUrls: ['./quick-access-widget.component.scss']
})
export class QuickAccessWidgetComponent implements OnInit {
  private router = inject(Router);

  quickLinks: QuickLink[] = [
    { label: 'Günlük Egzersizler', icon: 'today', route: '/student/daily-exercises', color: '#3f51b5' },
    {
      label: 'Geçmiş Egzersizler',
      icon: 'history',
      route: '/student/daily-exercises',
      color: '#e91e63',
      queryParams: { tab: 'history' }
    },
    { label: 'Raporlar', icon: 'assessment', route: '/student/reports', color: '#009688' },
    { label: 'Profilim', icon: 'person', route: '/student/profile', color: '#ff9800' }
  ];

  quotes: Quote[] = [
    { text: "Okumak, zekayı besleyen en önemli gıdadır.", author: "İlber Ortaylı" },
    { text: "Bir kitap, içinizdeki donmuş denizi parçalayacak bir balta olmalıdır.", author: "Franz Kafka" },
    { text: "Hızlı okuma, zamanı satın almanın en ucuz yoludur.", author: "Anonim" },
    { text: "Bugün okuduğun her sayfa, yarınki başarının temelidir.", author: "Anonim" },
    { text: "Zihin paraşüt gibidir, sadece açıldığında işe yarar.", author: "James Dewar" },
    { text: "Okumak, haz duymaya, zihnimizi süslemeye ve yetkimizi artırmaya yarar.", author: "Francis Bacon" },
    { text: "Kitap, zekayı kibarlaştırır.", author: "Cemil Meriç" }
  ];

  currentQuote: Quote = this.quotes[0];

  ngOnInit() {
    this.selectRandomQuote();
  }

  selectRandomQuote() {
    const index = Math.floor(Math.random() * this.quotes.length);
    this.currentQuote = this.quotes[index];
  }

  navigate(link: QuickLink) {
    this.router.navigate([link.route], { queryParams: link.queryParams });
  }
}
