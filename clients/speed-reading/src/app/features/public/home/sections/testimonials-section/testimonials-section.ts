import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { DEFAULT_HOME_PAGE_CONTENT, HomeApproachContent, HomeSectionHeading } from '../../home-page-content';

type ApproachContent = HomeSectionHeading & { items: HomeApproachContent[] };

@Component({
  selector: 'app-testimonials-section',
  standalone: true,
  imports: [CommonModule, MatCardModule],
  templateUrl: './testimonials-section.html',
  styleUrl: './testimonials-section.scss'
})
export class TestimonialsSectionComponent {
  @Input() content: ApproachContent = DEFAULT_HOME_PAGE_CONTENT.approach;
}
