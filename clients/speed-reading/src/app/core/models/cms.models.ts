export enum ContentBlockType {
    Text = 1,
    RichText = 2,
    Image = 3,
    Json = 4,
    Button = 5
}

export interface ContentBlock {
    id: string;
    key: string;
    group: string;
    label?: string;
    type: ContentBlockType;
    value: string;
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

export interface BlogPost {
    id: string;
    title: string;
    slug: string;
    summary?: string;  // Renamed from globalSummary
    content: string;
    coverImageUrl?: string;
    tags: string[]; // or string if comma separated
    author: string;
    isPublished: boolean;
    publishedAt?: string;
    viewCount: number;
    seoSettings: SeoSettings;
    createdAt?: string;
}

export interface NewsletterSubscriber {
    id: string;
    email: string;
    source?: string;
    isActive: boolean;
    createdAt: string;
}

export interface CreatePageRequest {
    title: string;
    slug?: string;
    content: string;
    isPublished: boolean;
    seoSettings: SeoSettings;
}

export interface UpdatePageRequest extends CreatePageRequest {
    id: string;
}

export interface CreateBlogPostRequest {
    title: string;
    slug?: string;
    summary?: string;
    content: string;
    coverImageUrl?: string;
    tags: string[];
    author: string;
    isPublished: boolean;
    publishedAt?: string;
    seoSettings: SeoSettings;
}

export interface BlogListResponse {
    items: BlogPost[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

export interface ContactMessage {
    id: string;
    name: string;
    email: string;
    subject: string;
    message: string;
    isRead: boolean;
    createdAt: string;
    isReplied: boolean;
    repliedAt?: string;
    replyContent?: string;
}

export interface ContactMessageListResponse {
    items: ContactMessage[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}
