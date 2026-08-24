import { Component, OnInit, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LegalDocumentService } from '../../../services/legal-document.service';
import { LegalDocumentType } from '../../../models/legal-document.model';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
    selector: 'app-cookies',
    standalone: true,
    imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule],
    templateUrl: './cookies.component.html',
    styleUrls: ['./cookies.component.scss']
})
export class CookiesComponent implements OnInit {
    currentDate = new Date();
    content: string = '';
    title = 'Çerez Politikası';
    loading = true;
    error = false;
    noDocument = false;

    constructor(
        private legalDocumentService: LegalDocumentService,
        private sanitizer: DomSanitizer
    ) { }

    ngOnInit(): void {
        this.loadDocument();
    }

    loadDocument(): void {
        this.loading = true;
        this.error = false;
        this.noDocument = false;


        this.legalDocumentService.getActiveDocuments(LegalDocumentType.CookiePolicy, 'tr').subscribe({
            next: (docs) => {
                if (docs && docs.length > 0) {
                    const doc = docs[0];
                    this.title = doc.title;
                    this.content = this.sanitizer.sanitize(SecurityContext.HTML, doc.content) || '';
                } else {
                    console.warn('No active cookie policy document found');
                    this.noDocument = true;
                }
                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading cookie policy:', err);
                this.error = true;
                this.loading = false;
            }
        });
    }
}
