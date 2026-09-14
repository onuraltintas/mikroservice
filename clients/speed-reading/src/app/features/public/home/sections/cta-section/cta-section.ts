import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

@Component({
  selector: 'app-cta-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: './cta-section.html',
  styleUrl: './cta-section.scss'
})
export class CtaSectionComponent {
  private router = inject(Router);
  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.cta;

  startTrial() {
    this.router.navigate(['/auth/register']);
  }
}
