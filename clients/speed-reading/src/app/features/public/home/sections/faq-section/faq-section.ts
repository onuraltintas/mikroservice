import { Component, Input, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { DEFAULT_HOME_PAGE_CONTENT, HomePageContent } from '../../home-page-content';

@Component({
  selector: 'app-faq-section',
  standalone: true,
  imports: [CommonModule, RouterLink, MatExpansionModule, MatIconModule],
  templateUrl: './faq-section.html',
  styleUrl: './faq-section.scss',
  encapsulation: ViewEncapsulation.None
})
export class FaqSectionComponent {
  @Input() content: HomePageContent['faq'] = DEFAULT_HOME_PAGE_CONTENT.faq;
}
