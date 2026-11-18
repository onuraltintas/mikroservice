import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DEFAULT_HOME_PAGE_CONTENT, HomeFeatureContent, HomeSectionHeading } from '../../home-page-content';

type FeaturesContent = HomeSectionHeading & { items: HomeFeatureContent[] };

@Component({
  selector: 'app-features-section',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './features-section.html',
  styleUrl: './features-section.scss'
})
export class FeaturesSectionComponent {
  @Input() content: FeaturesContent = DEFAULT_HOME_PAGE_CONTENT.features;
}
