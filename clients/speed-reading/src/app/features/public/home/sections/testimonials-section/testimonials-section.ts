import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DEFAULT_HOME_PAGE_CONTENT, HomeApproachContent, HomeSectionHeading } from '../../home-page-content';

interface Testimonial extends HomeApproachContent {
  rating: number;
  avatar: string;
}

type ApproachContent = HomeSectionHeading & { items: HomeApproachContent[] };

@Component({
  selector: 'app-testimonials-section',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './testimonials-section.html',
  styleUrl: './testimonials-section.scss'
})
export class TestimonialsSectionComponent {
  @Input() content: ApproachContent = DEFAULT_HOME_PAGE_CONTENT.approach;

  get testimonials(): Testimonial[] {
    return this.content.items.map(item => ({ ...item, rating: 5, avatar: this.getInitials(item.title) }));
  }

  private getInitials(name: string): string {
    return name
      .split(' ')
      .map(n => n[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  getStars(rating: number): number[] {
    return Array(5).fill(0).map((_, i) => i < rating ? 1 : 0);
  }
}
