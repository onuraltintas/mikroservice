import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-cookies',
    standalone: true,
    imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, NavbarComponent, FooterComponent],
    templateUrl: './cookies.component.html',
    styleUrls: ['./cookies.component.scss']
})
export class CookiesComponent {
    private readonly cmsService = inject(PublicCmsService);
    currentDate = new Date();
    content: string = '';
    title = 'Çerez Politikası';
    loading = true;
    error = false;
    noDocument = true;

    ngOnInit(): void {
        this.cmsService.getPage('cookies').pipe(finalize(() => this.loading = false)).subscribe({
            next: page => {
                if (page?.content?.trim()) {
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

}
