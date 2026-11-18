// CMS Models aligned with backend DTOs
export interface BlogPost {
    id: string;  // Guid from backend
    title: string;
    slug: string;
    summary: string;
    content: string;
    coverImageUrl?: string;
    tags: string[];
    author?: string;
    viewCount: number;
    isPublished: boolean;
    publishedAt?: string;  // ISO date string
    seoSettings: SeoSettings;
}

export interface SeoSettings {
    metaTitle?: string;
    metaDescription?: string;
    metaKeywords?: string;
    canonicalUrl?: string;
    ogTitle?: string;
    ogDescription?: string;
    ogImage?: string;
    noIndex: boolean;
}

export interface Page {
    id: string;
    title: string;
    slug: string;
    content: string;
    isPublished: boolean;
    seoSettings: SeoSettings;
    createdAt?: string;
    updatedAt?: string;
}

export interface Category {
    name: string;
    count: number;
}

// Helper function to calculate read time
export function calculateReadTime(content: string): string {
    const words = content.split(/\s+/).length;
    const minutes = Math.ceil(words / 200);  // 200 words per minute
    return `${minutes} dakika`;
}

// Helper function to get first tag as category
export function getCategory(tags: string[]): string {
    return tags.length > 0 ? tags[0] : 'Genel';
}

// Helper function to get category color (can be customized)
export function getCategoryColor(category: string): string {
    const colors: Record<string, string> = {
        'Teknikler': 'bg-(--ui-brand)',
        'Bilim': 'bg-(--ui-brand)',
        'Araştırma': 'bg-(--ui-success)',
        'İpuçları': 'bg-(--ui-warning)',
        'Motivasyon': 'bg-(--ui-danger)',
        'Teknoloji': 'bg-(--ui-brand)'
    };
    return colors[category] || 'bg-(--ui-text)';
}
