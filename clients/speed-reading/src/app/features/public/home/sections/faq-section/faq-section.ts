import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DEFAULT_HOME_PAGE_CONTENT, HomePageContent } from '../../home-page-content';

@Component({
  selector: 'app-faq-section',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './faq-section.html',
  styleUrl: './faq-section.scss'
})
export class FaqSectionComponent {
  @Input() content: HomePageContent['faq'] = DEFAULT_HOME_PAGE_CONTENT.faq;
}
