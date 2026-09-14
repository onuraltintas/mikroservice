import { Component, Input, OnInit, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

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
  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.faq;

  faqs: FaqItem[] = [];

  ngOnInit() {
    this.loadContent();
  }

  private loadContent() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        const faqContent = content.blocks['faq_items'] ?? content.blocks['faq_list'];
        if (faqContent) {
          try {
            const parsedFaqs = JSON.parse(faqContent);
            if (Array.isArray(parsedFaqs) && parsedFaqs.length > 0) {
              this.faqs = parsedFaqs.filter((faq): faq is FaqItem =>
                faq && typeof faq.question === 'string' && typeof faq.answer === 'string' && typeof faq.category === 'string').slice(0, 8);
            }
          } catch { /* CMS payload is ignored until corrected. */ }
        }
      }
    });
  }
}
