import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { SeoService } from '../../core/services/seo.service';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [CommonModule, RouterModule, MatButtonModule, MatIconModule, MatCardModule],
  templateUrl: './not-found.component.html',
  styleUrls: ['./not-found.component.scss']
})
export class NotFoundComponent implements OnInit, OnDestroy {
  private readonly seo = inject(SeoService);

  // Unknown addresses still answer 200 (single-page app), so the page tells
  // search engines not to index it; otherwise they are reported as soft 404s.
  ngOnInit(): void {
    this.seo.setNoIndex(true);
  }

  ngOnDestroy(): void {
    this.seo.setNoIndex(false);
  }

  goBack(): void {
    window.history.back();
  }
}
