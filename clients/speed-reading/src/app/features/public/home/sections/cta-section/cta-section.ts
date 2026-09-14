import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';

@Component({
  selector: 'app-cta-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: './cta-section.html',
  styleUrl: './cta-section.scss'
})
export class CtaSectionComponent {
  private router = inject(Router);

  title = 'Okuma Becerilerinizi Geliştirmeye Hazır mısınız?';
  subtitle = 'Başlangıç seviyenizi görün, düzenli çalışmalarla hız ve anlamayı birlikte takip edin.';
  buttonText = 'Hemen Başla';
  smallText = 'Sonuçlar başlangıç seviyesine ve düzenli çalışmaya göre değişir.';

  startTrial() {
    this.router.navigate(['/auth/register']);
  }
}
