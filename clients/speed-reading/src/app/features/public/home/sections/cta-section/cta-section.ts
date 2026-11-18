import { Component, Input, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

@Component({
  selector: 'app-cta-section',
  standalone: true,
  imports: [],
  templateUrl: './cta-section.html',
  styleUrl: './cta-section.scss'
})
export class CtaSectionComponent {
  private router = inject(Router);
  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.cta;

  startTrial() {
    this.router.navigateByUrl(this.content.actionUrl || '/auth/register');
  }
}
