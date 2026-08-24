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
  selector: 'app-terms',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './terms.component.html',
  styleUrls: ['./terms.component.scss']
})
export class TermsComponent implements OnInit {
  currentDate = new Date();
  content: string = '';
  title = 'Kullanım Koşulları';
  loading = true;
  error = false;

  constructor(
    private legalDocumentService: LegalDocumentService,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.loadDocument();
  }

  loadDocument(): void {
    this.loading = true;
    this.legalDocumentService.getActiveDocuments(LegalDocumentType.TermsOfService, 'tr').subscribe({
      next: (docs) => {
        if (docs && docs.length > 0) {
          const doc = docs[0];
          this.title = doc.title;
          this.content = this.sanitizer.sanitize(SecurityContext.HTML, doc.content) || '';
        } else {
          this.error = true;
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading terms:', err);
        this.error = true;
        this.loading = false;
      }
    });
  }
}
