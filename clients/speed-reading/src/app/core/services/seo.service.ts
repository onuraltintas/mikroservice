import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export interface SeoConfig {
    title?: string;
    description?: string;
    keywords?: string;
    image?: string;
    url?: string;
    canonicalUrl?: string;
    ogTitle?: string;
    ogDescription?: string;
    type?: string;
    author?: string;
    publishedTime?: string;
    modifiedTime?: string;
}

@Injectable({
    providedIn: 'root'
})
export class SeoService {
    private meta = inject(Meta);
    private title = inject(Title);

    private readonly defaultConfig: SeoConfig = {
        title: 'Hızlı Okuma Platformu',
        description: 'Okuma hızınızı artırın, anlama yeteneğinizi geliştirin. Bilimsel yöntemlerle hızlı okuma eğitimi.',
        keywords: 'hızlı okuma, speed reading, okuma hızı, anlama, eğitim',
        image: '/assets/images/og-image.jpg',
        type: 'website'
    };

    updateTags(config: SeoConfig): void {
        const seoConfig = { ...this.defaultConfig, ...config };

        const openGraphTitle = seoConfig.ogTitle || seoConfig.title;
        const openGraphDescription = seoConfig.ogDescription || seoConfig.description;

        // Title
        if (seoConfig.title) {
            this.title.setTitle(seoConfig.title);
            this.meta.updateTag({ property: 'og:title', content: openGraphTitle || seoConfig.title });
            this.meta.updateTag({ name: 'twitter:title', content: seoConfig.title });
        }

        // Description
        if (seoConfig.description) {
            this.meta.updateTag({ name: 'description', content: seoConfig.description });
            this.meta.updateTag({ property: 'og:description', content: openGraphDescription || seoConfig.description });
            this.meta.updateTag({ name: 'twitter:description', content: seoConfig.description });
        }

        // Keywords
        if (seoConfig.keywords) {
            this.meta.updateTag({ name: 'keywords', content: seoConfig.keywords });
        }

        // Image
        if (seoConfig.image) {
            this.meta.updateTag({ property: 'og:image', content: seoConfig.image });
            this.meta.updateTag({ name: 'twitter:image', content: seoConfig.image });
        }

        // URL
        const canonicalUrl = seoConfig.canonicalUrl || seoConfig.url;
        if (canonicalUrl) {
            this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
            this.meta.updateTag({ name: 'twitter:url', content: canonicalUrl });
            this.setCanonicalUrl(canonicalUrl);
        }

        // Type
        if (seoConfig.type) {
            this.meta.updateTag({ property: 'og:type', content: seoConfig.type });
        }

        // Author
        if (seoConfig.author) {
            this.meta.updateTag({ name: 'author', content: seoConfig.author });
        }

        // Article specific
        if (seoConfig.publishedTime) {
            this.meta.updateTag({ property: 'article:published_time', content: seoConfig.publishedTime });
        }

        if (seoConfig.modifiedTime) {
            this.meta.updateTag({ property: 'article:modified_time', content: seoConfig.modifiedTime });
        }

        // Twitter Card
        this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    }

    generateStructuredData(type: 'Article' | 'WebPage' | 'Organization', data: any): void {
        const scriptId = 'dynamic-structured-data';

        // Remove existing dynamic structured data
        const existingScript = document.getElementById(scriptId);
        if (existingScript) {
            existingScript.remove();
        }

        const script = document.createElement('script');
        script.id = scriptId;
        script.type = 'application/ld+json';

        let structuredData: any = {
            '@context': 'https://schema.org'
        };

        switch (type) {
            case 'Article':
                structuredData = {
                    ...structuredData,
                    '@type': 'Article',
                    headline: data.title,
                    description: data.description,
                    image: data.image,
                    author: {
                        '@type': 'Person',
                        name: data.author
                    },
                    publisher: {
                        '@type': 'Organization',
                        name: 'Hızlı Okuma Platformu',
                        logo: {
                            '@type': 'ImageObject',
                            url: '/assets/images/logo.png'
                        }
                    },
                    datePublished: data.publishedTime,
                    dateModified: data.modifiedTime || data.publishedTime
                };
                break;

            case 'WebPage':
                structuredData = {
                    ...structuredData,
                    '@type': 'WebPage',
                    name: data.title,
                    description: data.description,
                    url: data.url
                };
                break;

            case 'Organization':
                structuredData = {
                    ...structuredData,
                    '@type': 'Organization',
                    name: 'Hızlı Okuma Platformu',
                    url: 'https://hizliokuma.com',
                    logo: '/assets/images/logo.png',
                    sameAs: [
                        // Add social media URLs
                    ]
                };
                break;
        }

        script.text = JSON.stringify(structuredData);



        // Add new structured data
        document.head.appendChild(script);
    }

    setCanonicalUrl(url: string): void {
        let link: HTMLLinkElement | null = document.querySelector('link[rel="canonical"]');

        if (!link) {
            link = document.createElement('link');
            link.setAttribute('rel', 'canonical');
            document.head.appendChild(link);
        }

        link.setAttribute('href', url);
    }

    setNoIndex(noIndex: boolean): void {
        if (noIndex) {
            this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
        } else {
            this.meta.updateTag({ name: 'robots', content: 'index, follow' });
        }
    }
}
