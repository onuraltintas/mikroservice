/**
 * Sitemap Generator for Master Hızlı Okuma Platform
 * 
 * Usage: node generate-sitemap.js
 * 
 * This script generates sitemap.xml for SEO optimization.
 * Run this after deployment or when content changes.
 */

const fs = require('fs');
const path = require('path');

// Configuration
const BASE_URL = 'https://masterhizliokuma.com';
const OUTPUT_FILE = path.join(__dirname, 'public', 'sitemap.xml');

// Static pages with their priorities and change frequencies
const staticPages = [
    { url: '/', priority: 1.0, changefreq: 'daily' },
    { url: '/hakkimizda', priority: 0.8, changefreq: 'monthly' },
    { url: '/iletisim', priority: 0.7, changefreq: 'monthly' },
    { url: '/fiyatlandirma', priority: 0.9, changefreq: 'weekly' },
    { url: '/blog', priority: 0.9, changefreq: 'daily' },
    { url: '/auth/login', priority: 0.5, changefreq: 'yearly' },
    { url: '/auth/register', priority: 0.6, changefreq: 'yearly' },
    { url: '/legal/privacy', priority: 0.3, changefreq: 'yearly' },
    { url: '/legal/terms', priority: 0.3, changefreq: 'yearly' },
    { url: '/legal/kvkk', priority: 0.3, changefreq: 'yearly' },
    { url: '/legal/cookies', priority: 0.3, changefreq: 'yearly' },
];

// Generate XML for a single URL
function generateUrlXml(url, priority, changefreq, lastmod = new Date().toISOString().split('T')[0]) {
    return `
  <url>
    <loc>${BASE_URL}${url}</loc>
    <lastmod>${lastmod}</lastmod>
    <changefreq>${changefreq}</changefreq>
    <priority>${priority}</priority>
  </url>`;
}

// Generate complete sitemap XML
function generateSitemap(pages) {
    const urlsXml = pages.map(page =>
        generateUrlXml(page.url, page.priority, page.changefreq, page.lastmod)
    ).join('');

    return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:schemaLocation="http://www.sitemaps.org/schemas/sitemap/0.9
        http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd">
${urlsXml}
</urlset>`;
}

// Fetch dynamic content from API (blog posts, pages)
async function fetchDynamicContent() {
    const dynamicPages = [];

    // Note: In production, replace with actual API calls
    // Example:
    // const response = await fetch(`${API_URL}/api/blog-posts`);
    // const posts = await response.json();
    // posts.forEach(post => {
    //   dynamicPages.push({
    //     url: `/blog/${post.slug}`,
    //     priority: 0.7,
    //     changefreq: 'weekly',
    //     lastmod: post.updatedAt
    //   });
    // });

    console.log('📝 Dynamic content fetching is configured for API integration.');
    console.log('   Add your API endpoint to fetch blog posts and CMS pages.');

    return dynamicPages;
}

// Main execution
async function main() {
    console.log('🚀 Starting sitemap generation...');
    console.log(`📍 Base URL: ${BASE_URL}`);

    try {
        // Get dynamic content
        const dynamicPages = await fetchDynamicContent();

        // Combine static and dynamic pages
        const allPages = [...staticPages, ...dynamicPages];

        console.log(`📄 Total pages: ${allPages.length}`);

        // Generate sitemap
        const sitemap = generateSitemap(allPages);

        // Ensure directory exists
        const outputDir = path.dirname(OUTPUT_FILE);
        if (!fs.existsSync(outputDir)) {
            fs.mkdirSync(outputDir, { recursive: true });
        }

        // Write to file
        fs.writeFileSync(OUTPUT_FILE, sitemap, 'utf8');

        console.log(`✅ Sitemap generated successfully!`);
        console.log(`📂 Output: ${OUTPUT_FILE}`);
        console.log(`📊 Static pages: ${staticPages.length}`);
        console.log(`📊 Dynamic pages: ${dynamicPages.length}`);

    } catch (error) {
        console.error('❌ Error generating sitemap:', error.message);
        process.exit(1);
    }
}

main();
