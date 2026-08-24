import { Component, OnInit, inject } from '@angular/core';
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

    ngOnInit() {
        this.seoService.updateTags({
            title: 'Hızlı Okuma Platformu - Okuma Hızınızı 3 Katına Çıkarın',
            description: 'Bilimsel yöntemlerle okuma hızınızı artırın, anlama yeteneğinizi geliştirin. 50,000+ kullanıcımıza katılın!',
            url: window.location.href,
            type: 'website'
        });
    }
}
