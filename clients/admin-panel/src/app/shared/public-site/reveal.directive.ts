import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Directive, ElementRef, OnDestroy, OnInit, PLATFORM_ID, inject, input } from '@angular/core';

/**
 * Lets a block rise into place the first time it scrolls into view (public sites, 27.11.2026). Content is visible by
 * default: the hidden starting state is added only in a browser, only to blocks still below the fold, and never when
 * the visitor asked for reduced motion, so search engines, printing and no-JS visitors see everything.
 * `publicReveal` takes an optional order number for a short stagger inside a row of cards.
 */
@Directive({ selector: '[publicReveal]', standalone: true })
export class RevealDirective implements OnInit, OnDestroy {
  readonly publicReveal = input<number | string | null | undefined>(0);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private observer?: IntersectionObserver;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const view = this.document.defaultView;
    if (!view || typeof view.IntersectionObserver !== 'function') return;
    if (view.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;
    const node = this.element.nativeElement;
    if (node.getBoundingClientRect().top < view.innerHeight * 0.92) return;
    const order = Math.min(Math.max(Number(this.publicReveal()) || 0, 0), 6);
    node.style.setProperty('--reveal-delay', `${order * 70}ms`);
    node.classList.add('reveal-pending');
    this.observer = new view.IntersectionObserver(entries => {
      if (!entries.some(entry => entry.isIntersecting)) return;
      node.classList.add('reveal-run');
      view.requestAnimationFrame(() => node.classList.remove('reveal-pending'));
      this.observer?.disconnect();
    }, { rootMargin: '0px 0px -8% 0px' });
    this.observer.observe(node);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
