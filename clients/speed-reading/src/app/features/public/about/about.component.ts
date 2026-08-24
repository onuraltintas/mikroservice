import { Component, OnInit, inject, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PublicCmsService, LandingContentVm } from '../../../core/services/public-cms.service';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { DomSanitizer } from '@angular/platform-browser';
import { SeoService } from '../../../core/services/seo.service';

export interface Testimonial {
    name: string;
    role: string;
    text: string;
    rating: number;
}

@Component({
    selector: 'app-about',
    standalone: true,
    imports: [CommonModule, NavbarComponent, FooterComponent],
    templateUrl: './about.component.html',
    styleUrl: './about.component.scss'
})
export class AboutComponent implements OnInit {
    private cmsService = inject(PublicCmsService);
    private sanitizer = inject(DomSanitizer);
    private seoService = inject(SeoService);

    heroTitle = 'Hakkımızda';
    heroSubtitle = '';

    storyTitle = '';
    storyContent: string = '';

    missionTitle = '';
    missionContent: string = '';

    testimonials: Testimonial[] = [];

    loading = true;

    ngOnInit() {
        this.loadContent();
    }

    getStars(rating: number): number[] {
        return Array(5).fill(0).map((_, i) => i < rating ? 1 : 0);
    }

    private loadContent() {
        this.cmsService.getLandingContent('AboutPage').subscribe({
            next: (content: LandingContentVm) => {
                if (content.blocks['about_hero_title']) {
                    this.heroTitle = content.blocks['about_hero_title'];
                }
                if (content.blocks['about_hero_subtitle']) {
                    this.heroSubtitle = content.blocks['about_hero_subtitle'];
                }

                this.seoService.updateTags({
                    title: (this.heroTitle || 'Hakkımızda') + ' | Hızlı Okuma',
                    description: this.heroSubtitle || 'Hızlı Okuma Platformu hakkında bilgi edinin.',
                    url: window.location.href,
                    type: 'website'
                });

                if (content.blocks['about_story_title']) {
                    this.storyTitle = content.blocks['about_story_title'];
                }
                if (content.blocks['about_story_content']) {
                    this.storyContent = this.sanitizer.sanitize(SecurityContext.HTML, content.blocks['about_story_content']) || '';
                }

                if (content.blocks['about_mission_title']) {
                    this.missionTitle = content.blocks['about_mission_title'];
                }
                if (content.blocks['about_mission_content']) {
                    this.missionContent = this.sanitizer.sanitize(SecurityContext.HTML, content.blocks['about_mission_content']) || '';
                }

                if (content.blocks['about_testimonials']) {
                    try {
                        this.testimonials = JSON.parse(content.blocks['about_testimonials']);
                    } catch (e) {
                        console.error('Failed to parse testimonials', e);
                    }
                }

                this.loading = false;
            },
            error: (err: any) => {
                console.warn('Failed to load about page content', err);
                this.loading = false;
            }
        });
    }
}
