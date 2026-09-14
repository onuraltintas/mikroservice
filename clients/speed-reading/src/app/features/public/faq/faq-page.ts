import { Component, OnInit, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { SeoService } from '../../../core/services/seo.service';

interface FaqItem {
    question: string;
    answer: string;
    category: string;
}

@Component({
    selector: 'app-faq-page',
    standalone: true,
    imports: [CommonModule, MatExpansionModule, MatIconModule, NavbarComponent, FooterComponent],
    templateUrl: './faq-page.html',
    styleUrl: './faq-page.scss',
    encapsulation: ViewEncapsulation.None
})
export class FaqPageComponent implements OnInit {
    private cmsService = inject(PublicCmsService);
    private seoService = inject(SeoService);

    faqs: FaqItem[] = [];
    categories: string[] = [];
    selectedCategory = 'Tümü';

    ngOnInit() {
        this.loadContent();
    }

    private loadContent() {
        this.cmsService.getLandingContent().subscribe({
            next: (content) => {
                this.seoService.updateTags({
                    title: content.blocks['faq_seo_title'] || 'Sık Sorulan Sorular | Master Hızlı Okuma',
                    description: content.blocks['faq_seo_description'] || 'Master Hızlı Okuma ve çalışma düzeni hakkında sık sorulan sorular.',
                    keywords: content.blocks['faq_seo_keywords'] || undefined,
                    url: window.location.href,
                    type: 'website'
                });
                const faqContent = content.blocks['faq_items'] ?? content.blocks['faq_list'];
                if (faqContent) {
                    try {
                        const parsedFaqs = JSON.parse(faqContent);
                        if (Array.isArray(parsedFaqs) && parsedFaqs.length > 0) {
                            this.faqs = parsedFaqs;
                            // Always show all categories including Diğer
                            this.categories = ['Tümü', 'Genel', 'Teknik', 'Fiyatlandırma', 'Sertifika', 'Destek', 'Diğer'];
                        }
                    } catch (e) {
                        console.warn('Failed to parse faq_list');
                    }
                }
            },
            error: (err) => {
                console.warn('Failed to load landing content', err);
            }
        });
    }

    get filteredFaqs(): FaqItem[] {
        if (this.selectedCategory === 'Tümü') {
            return this.faqs;
        }
        return this.faqs.filter(faq => faq.category === this.selectedCategory);
    }

    selectCategory(category: string) {
        this.selectedCategory = category;
    }
}
