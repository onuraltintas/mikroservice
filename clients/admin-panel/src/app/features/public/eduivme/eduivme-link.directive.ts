import { Directive, HostBinding, HostListener, Input, inject } from '@angular/core';
import { Router } from '@angular/router';
import { siteLink } from './eduivme-site-content';

/**
 * A link whose address comes from site content: a path on the site opens through the router, "#x" scrolls on the page,
 * an https address is an ordinary link. Anything else is dropped (the service accepts nothing else either).
 */
@Directive({ selector: 'a[eduivmeHref]', standalone: true })
export class EduivmeHrefDirective {
  private readonly router = inject(Router);
  private url: string | null = null;

  @Input({ required: true }) set eduivmeHref(value: string | null | undefined) {
    this.url = siteLink(value) ? value ?? null : null;
  }

  @HostBinding('attr.href') get href(): string | null { return this.url; }

  @HostListener('click', ['$event'])
  open(event: MouseEvent): void {
    const link = siteLink(this.url);
    if (link?.kind !== 'route' || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    void this.router.navigateByUrl(link.path);
  }
}
