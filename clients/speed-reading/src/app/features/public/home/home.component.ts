import { Component, OnInit, inject } from '@angular/core';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomePageContent, parseHomePageContent } from './home-page-content';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { SeoService } from '../../../core/services/seo.service';
import { HeroSectionComponent } from './sections/hero-section/hero-section';
import { FeaturesSectionComponent } from './sections/features-section/features-section';
import { StatsSectionComponent } from './sections/stats-section/stats-section';
import { TestimonialsSectionComponent } from './sections/testimonials-section/testimonials-section';
import { PricingSectionComponent } from './sections/pricing-section/pricing-section';
import { BlogSectionComponent } from './sections/blog-section/blog-section';
import { NewsletterSectionComponent } from './sections/newsletter-section/newsletter-section';
import { FaqSectionComponent } from './sections/faq-section/faq-section';
import { CtaSectionComponent } from './sections/cta-section/cta-section';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [
        CommonModule,
        NavbarComponent,
        HeroSectionComponent,
        FeaturesSectionComponent,
        StatsSectionComponent,
        TestimonialsSectionComponent,
        PricingSectionComponent,
        BlogSectionComponent,
        NewsletterSectionComponent,
        FaqSectionComponent,
        CtaSectionComponent,
        FooterComponent
    ],
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
    private seoService = inject(SeoService);
    private cmsService = inject(PublicCmsService);
    content: HomePageContent = DEFAULT_HOME_PAGE_CONTENT;

    ngOnInit() {
        this.cmsService.getLandingContent('HomePage').subscribe({
            next: landing => {
                this.content = parseHomePageContent(landing.blocks);
                this.updateSeo();
            },
            error: () => this.updateSeo()
        });
    }

    private updateSeo(): void {
        this.seoService.updateTags({
            title: this.content.seo.title,
            description: this.content.seo.description,
            keywords: this.content.seo.keywords,
            image: this.content.seo.ogImage || undefined,
            url: window.location.href,
            type: 'website'
        });
        this.seoService.generateStructuredData('Organization', { name: 'ONAL Yazılım ve Otomasyon' });
    }
}
