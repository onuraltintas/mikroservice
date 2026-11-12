import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { LegalPageSeo } from '../legal-page-seo';

@Component({
  selector: 'app-kvkk',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, NavbarComponent, FooterComponent],
  templateUrl: './kvkk.component.html',
  styleUrls: ['./kvkk.component.scss']
})
export class KvkkComponent implements OnInit, OnDestroy {
  private readonly legalPages = inject(PlatformLegalPagesService);
  private readonly legalSeo = inject(LegalPageSeo);
  private restoreSeo?: () => void;
  currentDate = new Date();
  content: string = '';
  title = 'KVKK Aydınlatma Metni';
  loading = true;
  error = false;
  noDocument = true;

  ngOnInit(): void {
    this.restoreSeo = this.legalSeo.apply({
      path: '/legal/kvkk',
      heading: 'KVKK Aydınlatma Metni',
      description: "Kişisel verilerin KVKK kapsamında hangi amaçlarla işlendiğini ve ilgili kişilerin başvuru haklarını açıklayan aydınlatma metni."
    });
    this.legalPages.getPage('kvkk').pipe(finalize(() => this.loading = false)).subscribe({
      next: page => {
        if (page?.isPublished && page.content?.trim()) {
          this.title = page.title || this.title;
          this.content = page.content;
          this.noDocument = false;
        }
      },
      error: (response: HttpErrorResponse) => {
        this.error = response.status !== 404;
      }
    });
  }

  ngOnDestroy(): void {
    this.restoreSeo?.();
  }
}
