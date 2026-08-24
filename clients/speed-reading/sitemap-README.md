# Sitemap Generator Script

This script generates a sitemap.xml for the CMS frontend.

## Usage

```bash
node generate-sitemap.js
```

## Output

Creates `sitemap.xml` in the `src` directory with:
- Static pages (home, blog, contact)
- Dynamic pages from CMS
- Blog posts from CMS

## Configuration

Update the `BASE_URL` constant with your production URL.
