import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { DEFAULT_HOME_PAGE_CONTENT, HomeFeatureContent, HomeSectionHeading } from '../../home-page-content';

type FeaturesContent = HomeSectionHeading & { items: HomeFeatureContent[] };

@Component({
  selector: 'app-features-section',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatCardModule],
  templateUrl: './features-section.html',
  styleUrl: './features-section.scss'
})
export class FeaturesSectionComponent {
  @Input() content: FeaturesContent = DEFAULT_HOME_PAGE_CONTENT.features;
}
