import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

@Component({
  selector: 'app-cta-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: './cta-section.html',
  styleUrl: './cta-section.scss'
})
export class CtaSectionComponent implements OnInit {
  private router = inject(Router);
  private cmsService = inject(PublicCmsService);

  title = 'Okuma Becerilerinizi Geliştirmeye Hazır mısınız?';
  subtitle = 'Bugün başlayın ve 30 gün içinde okuma hızınızı ikiye katlayın';
  buttonText = 'Hemen Başla';
  smallText = 'İlk 7 gün ücretsiz!';

  ngOnInit() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['cta_title'])       this.title      = content.blocks['cta_title'];
        if (content.blocks['cta_subtitle'])    this.subtitle   = content.blocks['cta_subtitle'];
        if (content.blocks['cta_button_text']) this.buttonText = content.blocks['cta_button_text'];
        if (content.blocks['cta_small_text'])  this.smallText  = content.blocks['cta_small_text'];
      }
    });
  }

  startTrial() {
    this.router.navigate(['/auth/register']);
  }
}
