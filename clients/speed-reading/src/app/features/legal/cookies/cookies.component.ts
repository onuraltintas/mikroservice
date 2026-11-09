import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { SeoService } from '../../../core/services/seo.service';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-cookies',
    standalone: true,
    imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, NavbarComponent, FooterComponent],
    templateUrl: './cookies.component.html',
    styleUrls: ['./cookies.component.scss']
})
export class CookiesComponent implements OnInit, OnDestroy {
    private readonly legalPages = inject(PlatformLegalPagesService);
    private readonly seo = inject(SeoService);
    currentDate = new Date();
    content: string = '';
    title = 'Çerez Politikası';
    loading = true;
    error = false;
    noDocument = true;
    private previousCanonical: string | null = null;

    ngOnInit(): void {
        // index.html ships the home page as the default canonical; this page is its own document.
        this.previousCanonical = document.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;
        this.seo.setCanonicalUrl(`${window.location.origin}/legal/cookies`);
        this.legalPages.getPage('cookies').pipe(finalize(() => this.loading = false)).subscribe({
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
        // Pages without their own canonical must not keep this page's URL after in-app navigation.
        this.seo.setCanonicalUrl(this.previousCanonical || 'https://masterhizliokuma.com/');
    }

}
