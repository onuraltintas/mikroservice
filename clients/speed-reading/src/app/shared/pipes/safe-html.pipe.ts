import { Pipe, PipeTransform, SecurityContext } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/**
 * Sanitizes HTML content using Angular's DomSanitizer.
 * Use this pipe for public-facing content that comes from CMS/API.
 * For admin-only preview content, bypassSecurityTrustHtml is acceptable.
 */
@Pipe({
  name: 'safeHtml',
  standalone: true
})
export class SafeHtmlPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(value: string | null | undefined): SafeHtml {
    if (!value) return '';
    return this.sanitizer.sanitize(SecurityContext.HTML, value) || '';
  }
}
